import client from './client'

export const getComments = (hotelId, params) =>
  client.get(`/comments/${hotelId}`, { params })

export const postComment = (hotelId, payload) =>
  client.post(`/comments/${hotelId}`, payload)
