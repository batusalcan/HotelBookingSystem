import client from './client'

export const sendChatMessage = (payload) =>
  client.post('/ai/chat', payload)
