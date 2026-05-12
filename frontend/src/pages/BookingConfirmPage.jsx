import { useLocation, Link } from 'react-router-dom'

export default function BookingConfirmPage() {
  const { state } = useLocation()

  if (!state?.booking) {
    return (
      <div className="max-w-lg mx-auto px-4 py-20 text-center">
        <p className="text-slate-400">No booking information found.</p>
        <Link to="/" className="mt-4 inline-block text-teal-600 underline text-sm">Return to home</Link>
      </div>
    )
  }

  const { booking, room } = state

  return (
    <div className="max-w-lg mx-auto px-4 py-16">
      <div className="bg-white rounded-2xl border border-slate-100 shadow-sm p-10 text-center">
        <div className="w-20 h-20 bg-teal-50 rounded-full flex items-center justify-center text-4xl mx-auto mb-5">
          ✅
        </div>
        <h1 className="text-2xl font-extrabold text-slate-800 mb-2">Booking Confirmed!</h1>
        <p className="text-slate-400 mb-8">Your reservation has been successfully created.</p>

        <div className="bg-slate-50 rounded-xl p-5 text-left flex flex-col gap-3 text-sm mb-8 border border-slate-100">
          <div className="flex justify-between items-center">
            <span className="text-slate-500">Booking ID</span>
            <span className="font-mono font-bold text-slate-700 text-xs bg-slate-100 px-2 py-1 rounded">
              {booking.bookingId}
            </span>
          </div>
          {room && (
            <div className="flex justify-between">
              <span className="text-slate-500">Room Type</span>
              <span className="font-semibold text-slate-700">{room.typeName}</span>
            </div>
          )}
          <div className="flex justify-between">
            <span className="text-slate-500">Status</span>
            <span className="bg-teal-100 text-teal-700 font-bold px-3 py-0.5 rounded-full text-xs">
              {booking.status}
            </span>
          </div>
        </div>

        <p className="text-slate-400 text-xs mb-8">
          A confirmation will be sent to your email address.
        </p>

        <Link
          to="/"
          className="bg-teal-600 text-white px-8 py-3 rounded-xl font-bold hover:bg-teal-700 transition shadow-sm inline-block"
        >
          Back to Home
        </Link>
      </div>
    </div>
  )
}
