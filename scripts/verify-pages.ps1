$files = Get-ChildItem pages,server -Recurse -File | Where-Object { $_.Extension -in '.js','.mjs' }
foreach ($file in $files) { node --check $file.FullName }
