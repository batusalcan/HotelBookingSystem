import { useState, useEffect } from 'react'
import { useParams, useSearchParams, useNavigate, Link } from 'react-router-dom'
import { getRoomDetail, adminGetRoomTypes, createBooking } from '../api/hotelApi'
import { useAuth } from '../context/AuthContext'
import CommentSection from '../components/CommentSection'

export default function HotelDetailPage() {
  const { hotelId } = useParams()
  const [searchParams] = useSearchParams()
  const { session } = useAuth()
  const navigate = useNavigate()

  const startDate  = searchParams.get('startDate') || ''
  const endDate    = searchParams.get('endDate') || ''
  const guestCount = parseInt(searchParams.get('guestCount') || '2')

  const [roomTypes, setRoomTypes] = useState([])
  const [selectedRoom, setSelectedRoom] = useState(null)
  const [roomDetail, setRoomDetail] = useState(null)
  const [loadingRoom, setLoadingRoom] = useState(false)
  const [booking, setBooking] = useState(false)
  const [bookingError, setBookingError] = useState('')

  useEffect(() => {
    adminGetRoomTypes(hotelId)
      .then(({ data }) => setRoomTypes(Array.isArray(data) ? data : data.data ?? []))
      .catch(() => setRoomTypes([]))
  }, [hotelId])

  const handleSelectRoom = async (rt) => {
    setSelectedRoom(rt)
    setRoomDetail(null)
    setBookingError('')
    if (!session) return
    setLoadingRoom(true)
    try {
      const { data } = await getRoomDetail(hotelId, rt.roomTypeId, { startDate, endDate })
      setRoomDetail(data.data)
    } catch {
      setRoomDetail(null)
    } finally {
      setLoadingRoom(false)
    }
  }

  const handleBook = async () => {
    if (!session) { navigate('/login'); return }
    if (!roomDetail) return
    setBooking(true)
    setBookingError('')
    try {
      const { data } = await createBooking({
        hotelId,
        roomTypeId:  selectedRoom.roomTypeId,
        inventoryId: roomDetail.inventoryId,
        startDate,
        endDate,
        guestCount,
        rowVersion: roomDetail.rowVersion,
      })
      navigate('/bookings/confirm', { state: { booking: data.data, hotelId, room: selectedRoom } })
    } catch (err) {
      const status = err.response?.status
      if (status === 409) setBookingError('This room was just booked by someone else. Please try again or choose another room.')
      else if (status === 401) navigate('/login')
      else setBookingError('Something went wrong while booking. Please try again.')
    } finally {
      setBooking(false)
    }
  }

  const nights = startDate && endDate
    ? Math.max(1, Math.round((new Date(endDate) - new Date(startDate)) / 86400000))
    : 1

  const displayPrice = (base) => {
    if (!base) return null
    return session ? (base * 0.85).toFixed(0) : base.toFixed(0)
  }

  return (
    <div className="max-w-4xl mx-auto px-4 py-8">
      <Link
        to={searchParams.toString() ? `/search?${searchParams.toString()}` : '/'}
        className="text-teal-600 text-sm font-medium hover:underline mb-6 inline-flex items-center gap-1"
      >
        ← Back to results
      </Link>

      <div className="bg-white rounded-2xl border border-slate-100 shadow-sm overflow-hidden mb-6">
        {/* Hotel image placeholder */}
        <div className="h-56 bg-gradient-to-br from-teal-100 to-teal-200 flex items-center justify-center">
          <span className="text-7xl">🏨</span>
        </div>

        <div className="p-6">
          <div className="flex items-start justify-between flex-wrap gap-3 mb-4">
            <div>
              <h1 className="text-2xl font-bold text-slate-800">Hotel Detail</h1>
              {startDate && endDate && (
                <p className="text-slate-400 text-sm mt-1">
                  {startDate} → {endDate} · {guestCount} {guestCount === 1 ? 'guest' : 'guests'} · {nights} {nights === 1 ? 'night' : 'nights'}
                </p>
              )}
            </div>
          </div>

          {/* Discount banner */}
          {session ? (
            <div className="bg-teal-50 border border-teal-200 rounded-xl px-4 py-2.5 text-teal-700 text-sm font-medium mb-5 inline-flex items-center gap-2">
              🎉 You are signed in — <strong>15% member discount</strong> applied to all prices
            </div>
          ) : (
            <div className="bg-amber-50 border border-amber-200 rounded-xl px-4 py-2.5 text-amber-700 text-sm mb-5 inline-flex items-center gap-2">
              <Link to="/login" className="underline font-semibold">Sign in</Link> to unlock 15% discount on all rooms
            </div>
          )}

          {/* Room types */}
          <h3 className="font-bold text-slate-700 mb-3">Select a Room Type</h3>
          {roomTypes.length === 0 && <p className="text-slate-400 text-sm">No room types available.</p>}

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-6">
            {roomTypes.map((rt) => {
              const price = displayPrice(rt.basePricePerNight)
              const totalPrice = price ? (parseFloat(price) * nights).toFixed(0) : null
              const selected = selectedRoom?.roomTypeId === rt.roomTypeId

              return (
                <button
                  key={rt.roomTypeId}
                  onClick={() => handleSelectRoom(rt)}
                  className={`text-left border-2 rounded-2xl p-4 transition-all ${
                    selected
                      ? 'border-teal-500 bg-teal-50 shadow-sm'
                      : 'border-slate-200 hover:border-teal-300 hover:bg-slate-50'
                  }`}
                >
                  <div className="font-bold text-slate-800">{rt.typeName}</div>
                  <div className="text-slate-400 text-xs mt-1">Up to {rt.maxGuests} guests</div>
                  {price && (
                    <div className="mt-3">
                      <span className="text-xl font-extrabold text-slate-800">{price} TL</span>
                      <span className="text-slate-400 text-xs"> / night</span>
                      {nights > 1 && (
                        <div className="text-xs text-teal-600 font-semibold mt-0.5">{totalPrice} TL total for {nights} nights</div>
                      )}
                    </div>
                  )}
                </button>
              )
            })}
          </div>

          {/* Booking summary */}
          {selectedRoom && (
            <div className="bg-slate-50 rounded-2xl p-5 border border-slate-200">
              <h4 className="font-bold text-slate-700 mb-3">Booking Summary</h4>
              <div className="text-sm text-slate-600 flex flex-col gap-1.5 mb-4">
                <div><span className="font-semibold text-slate-700">Room:</span> {selectedRoom.typeName}</div>
                <div><span className="font-semibold text-slate-700">Dates:</span> {startDate} → {endDate}</div>
                <div><span className="font-semibold text-slate-700">Guests:</span> {guestCount}</div>
                {loadingRoom && <div className="text-teal-500 text-xs animate-pulse">Checking availability...</div>}
                {!loadingRoom && roomDetail && (
                  <div className="text-teal-600 text-xs font-semibold">✓ {roomDetail.availableCount} rooms available</div>
                )}
                {!loadingRoom && !roomDetail && session && (
                  <div className="text-red-500 text-xs">Could not fetch availability for these dates.</div>
                )}
              </div>

              {bookingError && (
                <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl p-3 text-sm mb-4">
                  {bookingError}
                </div>
              )}

              {session ? (
                <button
                  onClick={handleBook}
                  disabled={booking || loadingRoom || !roomDetail}
                  className="bg-teal-600 text-white px-8 py-3 rounded-xl font-bold hover:bg-teal-700 disabled:opacity-40 transition shadow-sm"
                >
                  {booking ? 'Booking...' : 'Book Now'}
                </button>
              ) : (
                <Link
                  to="/login"
                  className="bg-teal-600 text-white px-8 py-3 rounded-xl font-bold hover:bg-teal-700 transition shadow-sm inline-block"
                >
                  Sign in to Book
                </Link>
              )}
            </div>
          )}
        </div>
      </div>

      <CommentSection hotelId={hotelId} />
    </div>
  )
}
