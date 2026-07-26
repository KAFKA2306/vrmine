const rooms = new Map();
const sessions = new Map();
const port = Number(Bun.env.PORT ?? 8787);

function roomFor(name) {
  const room = rooms.get(name) ?? { state: null, clients: new Map() };
  rooms.set(name, room);
  return room;
}

function send(client, message) {
  client.send(JSON.stringify(message));
}

function broadcast(room, message) {
  for (const client of room.clients.keys()) send(client, message);
}

function playerIndex(room) {
  const used = new Set([...room.clients.values()].map((session) => session.playerIndex));
  return [0, 1, 2, 3].find((index) => !used.has(index));
}

function broadcastState(room) {
  for (const [client, session] of room.clients) send(client, { type: 'state', state: room.state, playerIndex: session.playerIndex });
}

const server = Bun.serve({
  port,
  fetch(request, serverInstance) {
    const url = new URL(request.url);
    if (url.pathname !== '/abyss') return new Response('VRMine Abyss Invasion server');
    return serverInstance.upgrade(request) ? undefined : new Response('WebSocket upgrade required', { status: 426 });
  },
  websocket: {
    open() {},
    message(client, rawMessage) {
      const message = JSON.parse(String(rawMessage));
      if (message.type === 'join') {
        const room = roomFor(message.room);
        const index = playerIndex(room);
        if (index === undefined) return send(client, { type: 'error', message: 'このルームは満員です。' });
        const session = { roomName: message.room, clientId: message.clientId, name: message.name, playerIndex: index };
        sessions.set(client, session);
        room.clients.set(client, session);
        send(client, { type: 'ready', playerIndex: index, host: room.state === null, server: `ws://${server.hostname}:${server.port}/abyss` });
        if (room.state) {
          if (room.state.players[index].name.startsWith('教団')) room.state.players[index].name = message.name;
          broadcastState(room);
        } else {
          send(client, { type: 'waiting' });
        }
        return;
      }
      const session = sessions.get(client);
      const room = rooms.get(session.roomName);
      if (message.type === 'start' && room.state === null) {
        room.state = message.state;
        broadcastState(room);
        return;
      }
      if (message.type === 'state') {
        room.state = message.state;
        broadcastState(room);
      }
    },
    close(client) {
      const session = sessions.get(client);
      if (!session) return;
      const room = rooms.get(session.roomName);
      room.clients.delete(client);
      sessions.delete(client);
      if (room.clients.size === 0) rooms.delete(session.roomName);
    }
  }
});

console.log(`VRMine Abyss Invasion server listening on ${server.hostname}:${server.port}/abyss`);
