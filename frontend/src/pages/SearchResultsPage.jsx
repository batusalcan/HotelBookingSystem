import { useState, useEffect, lazy, Suspense } from 'react'
import { useSearchParams, Link } from 'react-router-dom'
import { searchHotels } from '../api/hotelApi'
import HotelCard from '../components/HotelCard'

const MapView = lazy(() => import('../components/MapView'))

const SORT_OPTIONS = [
  { value: 'price_asc',   label: 'Price: Low to High' },
  { value: 'price_desc',  label: 'Price: High to Low' },
  { value: 'rating_desc', label: 'Highest Rated' },
]

export default function SearchResultsPage() {
  const [searchParams] = useSearchParams()
  const [hotels, setHotels] = useState([])
  const [meta, setMeta] = useState({ page: 1, totalPages: 1, totalRecords: 0 })
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [showMap, setShowMap] = useState(false)
  const [sortBy, setSortBy] = useState('price_asc')

  const sp = Object.fromEntries(searchParams.entries())

  useEffect(() => {
    setLoading(true)
    setError(null)
    searchHotels({ ...sp, page, pageSize: 10 })
      .then(({ data }) => {
        setHotels(data.data ?? [])
        setMeta({ page: data.page, totalPages: data.totalPages, totalRecords: data.totalRecords })
      })
      .catch(() => setError('Something went wrong while searching. Please try again.'))
      .finally(() => setLoading(false))
  }, [searchParams.toString(), page])

  const sorted = [...hotels].sort((a, b) => {
    if (sortBy === 'price_asc')  return a.pricePerNight - b.pricePerNight
    if (sortBy === 'price_desc') return b.pricePerNight - a.pricePerNight
    if (sortBy === 'rating_desc') return b.rating - a.rating
    return 0
  })

  return (
    <div className="max-w-5xl mx-auto px-4 py-8">
      {/* Header */}
      <div className="mb-6 flex items-start justify-between flex-wrap gap-3">
        <div>
          <h2 className="text-2xl font-bold text-slate-800">
            Hotels in {sp.destination}
          </h2>
          <p className="text-slate-400 text-sm mt-1">
            {sp.startDate} → {sp.endDate} · {sp.guestCount} {sp.guestCount == 1 ? 'guest' : 'guests'}
            {!loading && ` · ${meta.totalRecords} result${meta.totalRecords !== 1 ? 's' : ''}`}
          </p>
        </div>

        <div className="flex items-center gap-3">
          <select
            value={sortBy}
            onChange={(e) => setSortBy(e.target.value)}
            className="border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-teal-400 bg-white shadow-sm"
          >
            {SORT_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
          <button
            onClick={() => setShowMap((s) => !s)}
            className="bg-teal-600 text-white px-4 py-2 rounded-xl text-sm font-semibold hover:bg-teal-700 transition flex items-center gap-2 shadow-sm"
          >
            🗺 {showMap ? 'Hide Map' : 'Show on Map'}
          </button>
        </div>
      </div>

      {/* Map */}
      {showMap && (
        <div className="mb-6 rounded-2xl overflow-hidden border border-slate-200 shadow-sm" style={{ height: '380px' }}>
          <Suspense fallback={<div className="h-full flex items-center justify-center bg-slate-100 text-slate-400">Loading map...</div>}>
            <MapView hotels={hotels} />
          </Suspense>
        </div>
      )}

      {/* Loading skeleton */}
      {loading && (
        <div className="flex flex-col gap-4">
          {[1, 2, 3].map((n) => (
            <div key={n} className="bg-white rounded-2xl border border-slate-100 h-40 animate-pulse" />
          ))}
        </div>
      )}

      {/* Error */}
      {!loading && error && (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-2xl p-6 text-center">
          {error}
        </div>
      )}

      {/* Empty state */}
      {!loading && !error && sorted.length === 0 && (
        <div className="text-center py-20 text-slate-400">
          <p className="text-6xl mb-4">🏨</p>
          <p className="text-lg font-semibold text-slate-600">No available hotels found for your search.</p>
          <p className="text-sm mt-1 mb-6">Try different dates, destination, or fewer guests.</p>
          <Link to="/" className="bg-teal-600 text-white px-6 py-2.5 rounded-xl font-semibold hover:bg-teal-700 transition text-sm">
            New Search
          </Link>
        </div>
      )}

      {/* Results */}
      {!loading && !error && sorted.length > 0 && (
        <div className="flex flex-col gap-4">
          {sorted.map((hotel) => (
            <HotelCard key={hotel.hotelId} hotel={hotel} searchParams={sp} />
          ))}
        </div>
      )}

      {/* Pagination */}
      {meta.totalPages > 1 && (
        <div className="flex justify-center gap-2 mt-10">
          {Array.from({ length: meta.totalPages }, (_, i) => i + 1).map((p) => (
            <button
              key={p}
              onClick={() => setPage(p)}
              className={`w-10 h-10 rounded-full text-sm font-bold transition ${
                p === meta.page
                  ? 'bg-teal-600 text-white shadow'
                  : 'bg-white border border-slate-200 text-slate-600 hover:bg-slate-50'
              }`}
            >
              {p}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
