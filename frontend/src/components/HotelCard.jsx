import { Link } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'

export default function HotelCard({ hotel, searchParams }) {
  const { session } = useAuth()

  const basePrice = hotel.pricePerNight ?? 0
  const price = session ? (basePrice * 0.85).toFixed(0) : basePrice.toFixed(0)
  const qs = searchParams ? `?${new URLSearchParams(searchParams).toString()}` : ''

  return (
    <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden hover:shadow-md transition-shadow flex flex-col md:flex-row">
      <div className="md:w-56 h-44 md:h-auto bg-slate-100 flex-shrink-0 flex items-center justify-center text-slate-300 text-sm overflow-hidden">
        {hotel.imageUrl
          ? <img src={hotel.imageUrl} alt={hotel.name} className="w-full h-full object-cover" />
          : <span className="text-4xl">🏨</span>}
      </div>

      <div className="flex-1 p-5 flex flex-col justify-between">
        <div>
          <h3 className="font-bold text-lg text-slate-800">{hotel.name}</h3>
          <p className="text-slate-400 text-sm mt-0.5 mb-3">📍 {hotel.location || hotel.destination}</p>
          <div className="flex items-center gap-2 text-sm">
            <span className="bg-teal-600 text-white px-2 py-0.5 rounded font-bold text-xs">
              {hotel.rating?.toFixed(1)}
            </span>
            <span className="text-slate-500 text-xs">{hotel.totalReviews} reviews</span>
          </div>
        </div>

        <div className="mt-4 flex items-end justify-between">
          <div>
            {session && (
              <span className="text-xs text-teal-600 font-semibold block mb-0.5">15% member discount applied</span>
            )}
            <span className="text-2xl font-extrabold text-slate-800">{price} TL</span>
            <span className="text-slate-400 text-sm"> / night</span>
          </div>
          <Link
            to={`/hotels/${hotel.hotelId}${qs}`}
            className="bg-teal-600 text-white px-5 py-2 rounded-xl font-semibold hover:bg-teal-700 transition text-sm shadow-sm"
          >
            View Details
          </Link>
        </div>
      </div>
    </div>
  )
}
