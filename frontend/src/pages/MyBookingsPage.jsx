import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import { getUserBookings, cancelBooking } from '../api/hotelApi'

export default function MyBookingsPage() {
  const [bookings, setBookings] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [cancellingId, setCancellingId] = useState(null)
  const [confirmId, setConfirmId] = useState(null)

  const load = () => {
    setLoading(true)
    setError(null)
    getUserBookings()
      .then(({ data }) => setBookings(data.data ?? []))
      .catch(() => setError('Could not load your bookings. Please try again.'))
      .finally(() => setLoading(false))
  }

  useEffect(() => { load() }, [])

  const handleCancel = async (bookingId) => {
    setCancellingId(bookingId)
    try {
      await cancelBooking(bookingId)
      setBookings((prev) =>
        prev.map((b) => b.bookingId === bookingId ? { ...b, status: 'Cancelled' } : b)
      )
    } catch {
      alert('Failed to cancel the booking. Please try again.')
    } finally {
      setCancellingId(null)
      setConfirmId(null)
    }
  }

  const nights = (checkIn, checkOut) =>
    Math.max(1, Math.round((new Date(checkOut) - new Date(checkIn)) / 86400000))

  return (
    <div className="max-w-3xl mx-auto px-4 py-8">
      <Link to="/" className="text-teal-600 text-sm font-medium hover:underline mb-5 inline-flex items-center gap-1">
        ← Back to Home
      </Link>

      <h1 className="text-2xl font-bold text-slate-800 mb-1">My Bookings</h1>
      <p className="text-slate-400 text-sm mb-6">All your hotel reservations in one place.</p>

      {loading && (
        <div className="flex flex-col gap-4">
          {[1, 2, 3].map((n) => (
            <div key={n} className="bg-white rounded-2xl border border-slate-100 h-36 animate-pulse" />
          ))}
        </div>
      )}

      {!loading && error && (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-2xl p-6 text-center">{error}</div>
      )}

      {!loading && !error && bookings.length === 0 && (
        <div className="text-center py-20 text-slate-400">
          <p className="text-6xl mb-4">🏨</p>
          <p className="text-lg font-semibold text-slate-600">No bookings yet.</p>
          <p className="text-sm mt-1 mb-6">Find your next stay and book at a great price.</p>
          <Link to="/" className="bg-teal-600 text-white px-6 py-2.5 rounded-xl font-bold hover:bg-teal-700 transition">
            Search Hotels
          </Link>
        </div>
      )}

      {!loading && !error && bookings.length > 0 && (
        <div className="flex flex-col gap-4">
          {bookings.map((b) => {
            const n = nights(b.checkInDate, b.checkOutDate)
            const cancelled = b.status === 'Cancelled'
            const isConfirming = confirmId === b.bookingId
            const isCancelling = cancellingId === b.bookingId

            return (
              <div
                key={b.bookingId}
                className={`bg-white rounded-2xl border shadow-sm p-5 flex flex-col gap-3 ${
                  cancelled ? 'border-slate-100 opacity-60' : 'border-slate-200'
                }`}
              >
                <div className="flex items-start justify-between gap-3 flex-wrap">
                  <div>
                    <h2 className="font-bold text-slate-800 text-lg leading-tight">{b.hotelName}</h2>
                    <p className="text-slate-500 text-sm mt-0.5">{b.roomTypeName} · {b.guestCount} {b.guestCount === 1 ? 'guest' : 'guests'}</p>
                  </div>
                  <span className={`text-xs font-semibold px-3 py-1 rounded-full ${
                    cancelled
                      ? 'bg-slate-100 text-slate-500'
                      : 'bg-teal-100 text-teal-700'
                  }`}>
                    {b.status}
                  </span>
                </div>

                <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 text-sm">
                  <div className="bg-slate-50 rounded-xl p-3">
                    <div className="text-xs text-slate-400 font-medium mb-0.5">Check-in</div>
                    <div className="font-semibold text-slate-700">{b.checkInDate}</div>
                  </div>
                  <div className="bg-slate-50 rounded-xl p-3">
                    <div className="text-xs text-slate-400 font-medium mb-0.5">Check-out</div>
                    <div className="font-semibold text-slate-700">{b.checkOutDate}</div>
                  </div>
                  <div className="bg-slate-50 rounded-xl p-3">
                    <div className="text-xs text-slate-400 font-medium mb-0.5">Nights</div>
                    <div className="font-semibold text-slate-700">{n}</div>
                  </div>
                  <div className="bg-slate-50 rounded-xl p-3">
                    <div className="text-xs text-slate-400 font-medium mb-0.5">Total</div>
                    <div className="font-semibold text-teal-700">{b.totalAmount?.toLocaleString()} TL</div>
                  </div>
                </div>

                <div className="text-xs text-slate-400">
                  Booked on {new Date(b.createdAt).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })}
                  {' · '}ID: <span className="font-mono">{b.bookingId.slice(0, 8)}…</span>
                </div>

                {!cancelled && (
                  <div className="flex items-center gap-3 pt-1">
                    {!isConfirming ? (
                      <button
                        onClick={() => setConfirmId(b.bookingId)}
                        className="text-red-600 border border-red-200 hover:bg-red-50 px-4 py-1.5 rounded-xl text-sm font-semibold transition"
                      >
                        Cancel Reservation
                      </button>
                    ) : (
                      <>
                        <span className="text-sm text-slate-600">Are you sure you want to cancel this reservation?</span>
                        <button
                          onClick={() => handleCancel(b.bookingId)}
                          disabled={isCancelling}
                          className="bg-red-600 text-white px-4 py-1.5 rounded-xl text-sm font-semibold hover:bg-red-700 disabled:opacity-50 transition"
                        >
                          {isCancelling ? 'Cancelling…' : 'Yes, Cancel'}
                        </button>
                        <button
                          onClick={() => setConfirmId(null)}
                          className="text-slate-500 text-sm hover:underline"
                        >
                          Keep it
                        </button>
                      </>
                    )}
                  </div>
                )}
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}
