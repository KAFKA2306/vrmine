#!/usr/bin/env python3
from __future__ import annotations
import argparse, hashlib, json, math, random, struct
from pathlib import Path

PLY_TYPES = {
    "char": ("b", 1), "int8": ("b", 1),
    "uchar": ("B", 1), "uint8": ("B", 1),
    "short": ("h", 2), "int16": ("h", 2),
    "ushort": ("H", 2), "uint16": ("H", 2),
    "int": ("i", 4), "int32": ("i", 4),
    "uint": ("I", 4), "uint32": ("I", 4),
    "float": ("f", 4), "float32": ("f", 4),
    "double": ("d", 8), "float64": ("d", 8),
}

def vsub(a,b): return (a[0]-b[0],a[1]-b[1],a[2]-b[2])
def vdot(a,b): return a[0]*b[0]+a[1]*b[1]+a[2]*b[2]
def vcross(a,b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def vnorm(a): return math.sqrt(max(0.0,vdot(a,a)))
def vscale(a,s): return (a[0]*s,a[1]*s,a[2]*s)
def vadd(a,b): return (a[0]+b[0],a[1]+b[1],a[2]+b[2])
def vunit(a):
    n=vnorm(a)
    return None if n < 1e-12 else vscale(a,1.0/n)
def clamp(x,a,b): return max(a,min(b,x))

def quat_from_to(a,b):
    a=vunit(a); b=vunit(b)
    if a is None or b is None: raise ValueError("zero vector")
    d=clamp(vdot(a,b),-1.0,1.0)
    if d > 1.0-1e-10: return (0.0,0.0,0.0,1.0)
    if d < -1.0+1e-10:
        axis=vunit(vcross(a,(1.0,0.0,0.0))) or vunit(vcross(a,(0.0,1.0,0.0)))
        return (axis[0],axis[1],axis[2],0.0)
    c=vcross(a,b); s=math.sqrt((1.0+d)*2.0); inv=1.0/s
    return (c[0]*inv,c[1]*inv,c[2]*inv,s*0.5)

def quat_rotate(q,v):
    x,y,z,w=q; qv=(x,y,z); t=vscale(vcross(qv,v),2.0)
    return vadd(v, vadd(vscale(t,w), vcross(qv,t)))

def angle_deg(a,b):
    ua=vunit(a); ub=vunit(b)
    if ua is None or ub is None: return float("nan")
    return math.degrees(math.acos(clamp(vdot(ua,ub),-1.0,1.0)))

def _read_header(f):
    first=f.readline()
    if first.strip()!=b"ply": raise ValueError("not a PLY file")
    fmt=None; vertex_count=None; vertex_props=[]; in_vertex=False
    while True:
        line=f.readline()
        if not line: raise ValueError("truncated PLY header")
        text=line.decode("ascii","strict").strip()
        if text=="end_header": break
        parts=text.split()
        if not parts or parts[0] in {"comment","obj_info"}: continue
        if parts[0]=="format": fmt=parts[1]
        elif parts[0]=="element":
            in_vertex = len(parts)>=3 and parts[1]=="vertex"
            if in_vertex: vertex_count=int(parts[2])
        elif parts[0]=="property" and in_vertex:
            if len(parts)>=2 and parts[1]=="list":
                raise ValueError("list property inside vertex element is unsupported")
            typ,name=parts[1],parts[2]
            if typ not in PLY_TYPES: raise ValueError(f"unsupported PLY type {typ}")
            vertex_props.append((name,typ))
    if fmt not in {"binary_little_endian","ascii"}: raise ValueError(f"unsupported PLY format {fmt}")
    if not vertex_count or vertex_count < 3: raise ValueError("vertex count < 3")
    names={n for n,_ in vertex_props}
    if not {"x","y","z"} <= names: raise ValueError("PLY vertex requires x/y/z")
    return fmt,vertex_count,vertex_props,f.tell()

def sample_ply_points(path,max_points=12000):
    path=Path(path)
    with path.open("rb") as f:
        fmt,count,props,data_offset=_read_header(f)
        stride=max(1, math.ceil(count/max_points))
        indices=range(0,count,stride)
        points=[]
        if fmt=="binary_little_endian":
            offsets={}; off=0
            for name,typ in props:
                offsets[name]=(off,PLY_TYPES[typ][0]); off += PLY_TYPES[typ][1]
            row_size=off
            for idx in indices:
                f.seek(data_offset + idx*row_size)
                row=f.read(row_size)
                if len(row)!=row_size: raise ValueError("truncated binary PLY data")
                xyz=[]
                for name in ("x","y","z"):
                    o,code=offsets[name]
                    xyz.append(struct.unpack_from("<"+code,row,o)[0])
                if all(math.isfinite(v) for v in xyz): points.append(tuple(float(v) for v in xyz))
        else:
            prop_index={name:i for i,(name,_) in enumerate(props)}
            wanted=set(indices); last=max(wanted)
            for idx in range(last+1):
                row=f.readline()
                if not row: raise ValueError("truncated ascii PLY data")
                if idx in wanted:
                    vals=row.decode("ascii","strict").split()
                    xyz=tuple(float(vals[prop_index[n]]) for n in ("x","y","z"))
                    if all(math.isfinite(v) for v in xyz): points.append(xyz)
    if len(points)<3: raise ValueError("not enough finite sampled points")
    return points,count

def robust_core(points, quantile=0.985):
    cx=sorted(p[0] for p in points); cy=sorted(p[1] for p in points); cz=sorted(p[2] for p in points)
    def med(xs): return xs[len(xs)//2]
    center=(med(cx),med(cy),med(cz))
    dist=sorted((vnorm(vsub(p,center)),i) for i,p in enumerate(points))
    keep=max(3,int(len(points)*quantile))
    return [points[i] for _,i in dist[:keep]]

def bbox_diag(points):
    mins=[min(p[i] for p in points) for i in range(3)]
    maxs=[max(p[i] for p in points) for i in range(3)]
    return vnorm(tuple(maxs[i]-mins[i] for i in range(3)))

def plane_from3(a,b,c):
    n=vunit(vcross(vsub(b,a),vsub(c,a)))
    if n is None: return None
    return n, -vdot(n,a)

def jacobi_smallest_eigenvector(cov, sweeps=32):
    a=[list(row) for row in cov]
    v=[[1.0,0.0,0.0],[0.0,1.0,0.0],[0.0,0.0,1.0]]
    for _ in range(sweeps):
        p,q=max(((0,1),(0,2),(1,2)), key=lambda ij: abs(a[ij[0]][ij[1]]))
        if abs(a[p][q])<1e-15: break
        phi=0.5*math.atan2(2*a[p][q],a[q][q]-a[p][p])
        c=math.cos(phi); s=math.sin(phi)
        app,aqq,apq=a[p][p],a[q][q],a[p][q]
        a[p][p]=c*c*app-2*s*c*apq+s*s*aqq
        a[q][q]=s*s*app+2*s*c*apq+c*c*aqq
        a[p][q]=a[q][p]=0.0
        for r in range(3):
            if r in (p,q): continue
            arp,arq=a[r][p],a[r][q]
            a[r][p]=a[p][r]=c*arp-s*arq
            a[r][q]=a[q][r]=s*arp+c*arq
        for r in range(3):
            vrp,vrq=v[r][p],v[r][q]
            v[r][p]=c*vrp-s*vrq; v[r][q]=s*vrp+c*vrq
    idx=min(range(3), key=lambda i:a[i][i])
    return vunit((v[0][idx],v[1][idx],v[2][idx]))

def refine_plane(points, inlier_indices):
    pts=[points[i] for i in inlier_indices]
    inv=1.0/len(pts)
    center=(sum(p[0] for p in pts)*inv,sum(p[1] for p in pts)*inv,sum(p[2] for p in pts)*inv)
    xx=xy=xz=yy=yz=zz=0.0
    for p in pts:
        x,y,z=vsub(p,center)
        xx+=x*x; xy+=x*y; xz+=x*z; yy+=y*y; yz+=y*z; zz+=z*z
    normal=jacobi_smallest_eigenvector(((xx,xy,xz),(xy,yy,yz),(xz,yz,zz)))
    if normal is None: raise ValueError("plane refinement failed")
    return normal,center

def fit_dominant_plane(points, seed, iterations=500, distance_ratio=0.006):
    pts=robust_core(points)
    diag=bbox_diag(pts)
    if not math.isfinite(diag) or diag<=1e-9: raise ValueError("degenerate point cloud bounds")
    threshold=max(diag*distance_ratio,1e-7)
    rng=random.Random(seed)
    best=None
    npts=len(pts)
    for _ in range(iterations):
        i,j,k=rng.sample(range(npts),3)
        model=plane_from3(pts[i],pts[j],pts[k])
        if model is None: continue
        n,d=model
        inliers=[idx for idx,p in enumerate(pts) if abs(vdot(n,p)+d)<=threshold]
        if best is None or len(inliers)>len(best):
            best=inliers
    if not best or len(best)<3: raise ValueError("RANSAC found no plane")
    n,center=refine_plane(pts,best)
    residuals=sorted(abs(vdot(n,vsub(pts[i],center))) for i in best)
    rms=math.sqrt(sum(r*r for r in residuals)/len(residuals))
    return {
        "normal":n, "center":center, "inliers":len(best), "sampled":len(pts),
        "inlier_ratio":len(best)/len(pts), "threshold":threshold, "rms":rms, "bbox_diag":diag
    }

def infer_mode(title):
    t=(title or "").lower()
    wall_terms=("fachada","facade","façade","frontage")
    return "wall" if any(term in t for term in wall_terms) else "horizon"

def analyze(points, source_id, mode="horizon", apply_threshold_deg=1.0, min_inlier_ratio=0.08):
    seed=int.from_bytes(hashlib.sha256(source_id.encode()).digest()[:8],"big")
    plane=fit_dominant_plane(points,seed)
    normal=plane["normal"]
    if mode=="horizon":
        if normal[1]<0: normal=vscale(normal,-1)
        target=(0.0,1.0,0.0)
    elif mode=="wall":
        if normal[0]<0: normal=vscale(normal,-1)
        target=(1.0,0.0,0.0)
    else:
        raise ValueError("mode must be horizon or wall")
    tilt=angle_deg(normal,target)
    q=quat_from_to(normal,target)
    corrected=quat_rotate(q,normal)
    residual=angle_deg(corrected,target)
    geometric_pass = plane["inlier_ratio"]>=min_inlier_ratio and residual<=1e-4
    confidence="accepted" if geometric_pass and mode=="horizon" else "review_required"
    action="apply" if confidence=="accepted" and tilt>=apply_threshold_deg else "no_op" if confidence=="accepted" else "review_required"
    return {
        "id":source_id, "mode":mode, "status":confidence, "action":action,
        "tilt_deg":tilt, "post_alignment_residual_deg":residual,
        "plane":{"normal":[*normal],"center":[*plane["center"]],"inliers":plane["inliers"],"sampled":plane["sampled"],
                 "inlier_ratio":plane["inlier_ratio"],"distance_threshold":plane["threshold"],"rms_residual":plane["rms"],"bbox_diag":plane["bbox_diag"]},
        "alignment":{"rotation":{"x":q[0],"y":q[1],"z":q[2],"w":q[3]},"pivot":{"x":plane["center"][0],"y":plane["center"][1],"z":plane["center"][2]}}
    }

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("--registry",default="config/gaussian-splats.json")
    ap.add_argument("--source-dir",default="Library/VRMine/GaussianSources")
    ap.add_argument("--output",default="Library/VRMine/gaussian-orientation-evidence.json")
    ap.add_argument("--max-points",type=int,default=12000)
    ap.add_argument("--mode",choices=("auto","horizon","wall"),default="auto")
    ap.add_argument("--apply-threshold-deg",type=float,default=1.0)
    ap.add_argument("--min-inlier-ratio",type=float,default=0.08)
    args=ap.parse_args()
    registry=json.loads(Path(args.registry).read_text(encoding="utf-8"))
    results=[]
    for env in registry["environments"]:
        sid=env["id"]; title=((env.get("source") or {}).get("provenance") or {}).get("title","")
        mode=infer_mode(title) if args.mode=="auto" else args.mode
        file=Path(args.source_dir)/f"{sid}.ply"
        points,total=sample_ply_points(file,args.max_points)
        result=analyze(points,sid,mode,args.apply_threshold_deg,args.min_inlier_ratio)
        result["source"]={"path":str(file),"vertex_count":total,"sample_count":len(points),"title":title}
        if mode=="wall":
            result["semantic_limit"]="wall normal constrains wall verticality/front direction but does not prove roll around the wall normal"
        results.append(result)
        print(f'{sid}: mode={mode} action={result["action"]} tilt={result["tilt_deg"]:.3f}deg inliers={result["plane"]["inlier_ratio"]:.3f} residual={result["post_alignment_residual_deg"]:.6f}deg')
    payload={"schema_version":1,"method":"deterministic-ransac-plane+least-squares-refine","results":results}
    out=Path(args.output); out.parent.mkdir(parents=True,exist_ok=True)
    out.write_text(json.dumps(payload,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    if len(results)!=len(registry["environments"]): raise SystemExit("orientation evidence count mismatch")
    if any(r["status"]=="review_required" for r in results): raise SystemExit("one or more sources require orientation review")

if __name__=="__main__":
    main()
