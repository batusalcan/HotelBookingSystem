import client from './client'

export const searchHotels = (params) =>
  client.get('/search/hotels', { params })

export const getHotelDetail = (hotelId) =>
  client.get(`/hotels/${hotelId}`)

export const getHotelRoomTypes = (hotelId) =>
  client.get(`/hotels/${hotelId}/roomtypes`)

export const getRoomDetail = (hotelId, roomTypeId, params) =>
  client.get(`/hotels/${hotelId}/rooms/${roomTypeId}`, { params })

export const createBooking = (payload) =>
  client.post('/bookings', payload)

export const getUserBookings = () =>
  client.get('/bookings')

export const cancelBooking = (bookingId) =>
  client.delete(`/bookings/${bookingId}`)

// Admin
export const adminListHotels = (params) =>
  client.get('/admin/hotels', { params })

export const adminCreateHotel = (payload) =>
  client.post('/admin/hotels', payload)

export const adminUpdateHotel = (hotelId, payload) =>
  client.put(`/admin/hotels/${hotelId}`, payload)

export const adminGetRoomTypes = (hotelId) =>
  client.get(`/admin/hotels/${hotelId}/roomtypes`)

export const adminCreateRoomType = (hotelId, payload) =>
  client.post(`/admin/hotels/${hotelId}/roomtypes`, payload)

export const adminDeleteHotel = (hotelId) =>
  client.delete(`/admin/hotels/${hotelId}`)

export const adminUpsertInventory = (payload) =>
  client.post('/admin/inventory', payload)

export const adminGetCapacityReport = (days = 30) =>
  client.get('/admin/hotels/capacity-report', { params: { days } })

export const adminGetNotifications = (page = 1, pageSize = 50) =>
  client.get('/notifications', { params: { page, pageSize } })

export const markNotificationRead = (id) =>
  client.patch(`/notifications/${id}/read`)
