import { MapContainer, TileLayer, Marker, Popup } from 'react-leaflet'
import { Link } from 'react-router-dom'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'

delete L.Icon.Default.prototype._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
})

export default function MapView({ hotels }) {
  const valid = hotels.filter((h) => h.coordinates?.lat && h.coordinates?.lng)

  if (valid.length === 0) {
    return (
      <div className="h-full flex items-center justify-center bg-slate-100 text-slate-400 rounded-2xl text-sm">
        No map data available
      </div>
    )
  }

  const center = [valid[0].coordinates.lat, valid[0].coordinates.lng]

  return (
    <MapContainer center={center} zoom={8} className="h-full w-full rounded-2xl z-0">
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />
      {valid.map((hotel) => (
        <Marker key={hotel.hotelId} position={[hotel.coordinates.lat, hotel.coordinates.lng]}>
          <Popup>
            <div className="text-sm">
              <p className="font-bold text-slate-800">{hotel.name}</p>
              <p className="text-slate-500">{hotel.pricePerNight?.toFixed(0)} TL / night</p>
              <Link to={`/hotels/${hotel.hotelId}`} className="text-teal-600 underline text-xs">View details</Link>
            </div>
          </Popup>
        </Marker>
      ))}
    </MapContainer>
  )
}
