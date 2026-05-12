import { useState, useEffect } from 'react'
import {
  adminListHotels,
  adminCreateHotel,
  adminGetRoomTypes,
  adminCreateRoomType,
  adminUpsertInventory,
} from '../api/hotelApi'

const today = new Date().toISOString().split('T')[0]

export default function AdminPage() {
  const [tab, setTab] = useState('hotels')
  const [hotels, setHotels] = useState([])
  const [loadingHotels, setLoadingHotels] = useState(true)
  const [msg, setMsg] = useState({ text: '', ok: true })

  const [hotelForm, setHotelForm] = useState({ name: '', destination: '', latitude: '', longitude: '', baseRating: '8.0', imageUrl: '' })
  const [selectedHotelId, setSelectedHotelId] = useState('')
  const [roomTypes, setRoomTypes] = useState([])
  const [rtForm, setRtForm] = useState({ typeName: '', maxGuests: '2', basePricePerNight: '' })
  const [selectedRtId, setSelectedRtId] = useState('')
  const [invForm, setInvForm] = useState({ startDate: today, endDate: '', availableCount: '', isAvailable: true })

  const loadHotels = () => {
    setLoadingHotels(true)
    adminListHotels({ page: 1, pageSize: 100 })
      .then(({ data }) => setHotels(data.data ?? []))
      .catch(() => setHotels([]))
      .finally(() => setLoadingHotels(false))
  }

  useEffect(() => { loadHotels() }, [])

  useEffect(() => {
    if (!selectedHotelId) { setRoomTypes([]); return }
    adminGetRoomTypes(selectedHotelId)
      .then(({ data }) => setRoomTypes(Array.isArray(data) ? data : data.data ?? []))
      .catch(() => setRoomTypes([]))
  }, [selectedHotelId])

  const flash = (text, ok = true) => { setMsg({ text, ok }); setTimeout(() => setMsg({ text: '', ok: true }), 3500) }

  const handleCreateHotel = async (e) => {
    e.preventDefault()
    try {
      await adminCreateHotel({
        name: hotelForm.name,
        destination: hotelForm.destination,
        latitude: parseFloat(hotelForm.latitude),
        longitude: parseFloat(hotelForm.longitude),
        baseRating: parseFloat(hotelForm.baseRating),
        imageUrl: hotelForm.imageUrl || null,
      })
      flash('Hotel created successfully.')
      setHotelForm({ name: '', destination: '', latitude: '', longitude: '', baseRating: '8.0', imageUrl: '' })
      loadHotels()
    } catch {
      flash('Failed to create hotel.', false)
    }
  }

  const handleCreateRoomType = async (e) => {
    e.preventDefault()
    if (!selectedHotelId) { flash('Please select a hotel first.', false); return }
    try {
      await adminCreateRoomType(selectedHotelId, {
        typeName: rtForm.typeName,
        maxGuests: parseInt(rtForm.maxGuests),
        basePricePerNight: parseFloat(rtForm.basePricePerNight),
      })
      flash('Room type created.')
      setRtForm({ typeName: '', maxGuests: '2', basePricePerNight: '' })
      adminGetRoomTypes(selectedHotelId).then(({ data }) => setRoomTypes(Array.isArray(data) ? data : data.data ?? []))
    } catch {
      flash('Failed to create room type.', false)
    }
  }

  const handleUpsertInventory = async (e) => {
    e.preventDefault()
    if (!selectedHotelId || !selectedRtId) { flash('Select a hotel and room type.', false); return }
    try {
      await adminUpsertInventory({
        hotelId: selectedHotelId,
        roomTypeId: selectedRtId,
        startDate: invForm.startDate,
        endDate: invForm.endDate,
        availableCount: parseInt(invForm.availableCount),
        isAvailable: invForm.isAvailable,
      })
      flash('Inventory updated.')
      setInvForm({ startDate: today, endDate: '', availableCount: '', isAvailable: true })
    } catch (err) {
      flash(err.response?.data?.message ?? 'Failed to update inventory.', false)
    }
  }

  const set = (setter, key) => (e) => setter((f) => ({ ...f, [key]: e.target.value }))

  const TABS = [
    { key: 'hotels',    label: '🏨 Hotels' },
    { key: 'roomtypes', label: '🛏 Room Types' },
    { key: 'inventory', label: '📅 Inventory' },
  ]

  return (
    <div className="max-w-4xl mx-auto px-4 py-8">
      <h1 className="text-2xl font-extrabold text-slate-800 mb-1">Admin Panel</h1>
      <p className="text-slate-400 text-sm mb-6">Manage hotels, room types, and availability</p>

      {msg.text && (
        <div className={`mb-5 rounded-xl px-4 py-3 text-sm font-medium border ${
          msg.ok ? 'bg-teal-50 border-teal-200 text-teal-700' : 'bg-red-50 border-red-200 text-red-700'
        }`}>
          {msg.text}
        </div>
      )}

      {/* Tabs */}
      <div className="flex gap-1 mb-6 border-b border-slate-200">
        {TABS.map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={`px-5 py-2.5 text-sm font-semibold rounded-t-xl transition ${
              tab === t.key
                ? 'bg-white border border-b-white border-slate-200 text-teal-600 -mb-px'
                : 'text-slate-400 hover:text-slate-700'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {/* Hotels tab */}
      {tab === 'hotels' && (
        <div className="flex flex-col gap-6">
          <div className="bg-white rounded-2xl border border-slate-100 p-6 shadow-sm">
            <h2 className="font-bold text-slate-700 mb-4">Add New Hotel</h2>
            <form onSubmit={handleCreateHotel} className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <FormInput label="Hotel Name" required value={hotelForm.name} onChange={set(setHotelForm, 'name')} />
              <FormInput label="City / Destination" required value={hotelForm.destination} onChange={set(setHotelForm, 'destination')} />
              <FormInput label="Latitude" type="number" step="0.0001" required value={hotelForm.latitude} onChange={set(setHotelForm, 'latitude')} />
              <FormInput label="Longitude" type="number" step="0.0001" required value={hotelForm.longitude} onChange={set(setHotelForm, 'longitude')} />
              <FormInput label="Base Rating (0–10)" type="number" min="0" max="10" step="0.1" required value={hotelForm.baseRating} onChange={set(setHotelForm, 'baseRating')} />
              <FormInput label="Image URL (optional)" value={hotelForm.imageUrl} onChange={set(setHotelForm, 'imageUrl')} placeholder="https://..." />
              <div className="sm:col-span-2">
                <button type="submit" className="bg-teal-600 text-white px-5 py-2 rounded-xl font-semibold hover:bg-teal-700 transition text-sm shadow-sm">
                  Create Hotel
                </button>
              </div>
            </form>
          </div>

          <div className="bg-white rounded-2xl border border-slate-100 p-6 shadow-sm">
            <h2 className="font-bold text-slate-700 mb-4">Existing Hotels</h2>
            {loadingHotels && <p className="text-slate-400 text-sm animate-pulse">Loading...</p>}
            {!loadingHotels && hotels.length === 0 && <p className="text-slate-400 text-sm">No hotels yet.</p>}
            <div className="flex flex-col gap-2">
              {hotels.map((h) => (
                <div key={h.hotelId} className="flex items-center justify-between bg-slate-50 rounded-xl px-4 py-3 border border-slate-100">
                  <div>
                    <span className="font-semibold text-slate-800">{h.name}</span>
                    <span className="text-slate-400 text-xs ml-2">{h.destination}</span>
                  </div>
                  <span className="text-xs text-slate-300 font-mono">{h.hotelId?.slice(0, 8)}…</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Room types tab */}
      {tab === 'roomtypes' && (
        <div className="flex flex-col gap-6">
          <div className="bg-white rounded-2xl border border-slate-100 p-6 shadow-sm">
            <h2 className="font-bold text-slate-700 mb-4">Add Room Type</h2>
            <div className="mb-4">
              <label className="block text-xs font-bold text-slate-500 uppercase tracking-wide mb-1">Select Hotel</label>
              <select value={selectedHotelId} onChange={(e) => setSelectedHotelId(e.target.value)}
                className="border border-slate-200 rounded-xl px-3 py-2 text-sm w-full max-w-sm focus:outline-none focus:ring-2 focus:ring-teal-400 bg-slate-50">
                <option value="">-- Choose a hotel --</option>
                {hotels.map((h) => <option key={h.hotelId} value={h.hotelId}>{h.name}</option>)}
              </select>
            </div>
            <form onSubmit={handleCreateRoomType} className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <FormInput label="Room Type Name" required value={rtForm.typeName} onChange={set(setRtForm, 'typeName')} placeholder="e.g. Standard, Family" />
              <FormInput label="Max Guests" type="number" min="1" required value={rtForm.maxGuests} onChange={set(setRtForm, 'maxGuests')} />
              <FormInput label="Price per Night (TL)" type="number" min="0" step="0.01" required value={rtForm.basePricePerNight} onChange={set(setRtForm, 'basePricePerNight')} />
              <div className="sm:col-span-3">
                <button type="submit" className="bg-teal-600 text-white px-5 py-2 rounded-xl font-semibold hover:bg-teal-700 transition text-sm shadow-sm">
                  Create Room Type
                </button>
              </div>
            </form>
          </div>

          {selectedHotelId && roomTypes.length > 0 && (
            <div className="bg-white rounded-2xl border border-slate-100 p-6 shadow-sm">
              <h2 className="font-bold text-slate-700 mb-3">Existing Room Types</h2>
              <div className="flex flex-col gap-2">
                {roomTypes.map((rt) => (
                  <div key={rt.roomTypeId} className="bg-slate-50 rounded-xl px-4 py-3 border border-slate-100 flex justify-between items-center">
                    <div>
                      <span className="font-semibold text-slate-800">{rt.typeName}</span>
                      <span className="text-slate-400 text-xs ml-2">Max {rt.maxGuests} guests</span>
                    </div>
                    <span className="text-slate-700 font-bold text-sm">{rt.basePricePerNight?.toFixed(0)} TL / night</span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Inventory tab */}
      {tab === 'inventory' && (
        <div className="bg-white rounded-2xl border border-slate-100 p-6 shadow-sm">
          <h2 className="font-bold text-slate-700 mb-4">Update Availability</h2>
          <div className="mb-4">
            <label className="block text-xs font-bold text-slate-500 uppercase tracking-wide mb-1">Select Hotel</label>
            <select value={selectedHotelId} onChange={(e) => { setSelectedHotelId(e.target.value); setSelectedRtId('') }}
              className="border border-slate-200 rounded-xl px-3 py-2 text-sm w-full max-w-sm focus:outline-none focus:ring-2 focus:ring-teal-400 bg-slate-50">
              <option value="">-- Choose a hotel --</option>
              {hotels.map((h) => <option key={h.hotelId} value={h.hotelId}>{h.name}</option>)}
            </select>
          </div>

          {selectedHotelId && (
            <div className="mb-5">
              <label className="block text-xs font-bold text-slate-500 uppercase tracking-wide mb-1">Room Type</label>
              <select value={selectedRtId} onChange={(e) => setSelectedRtId(e.target.value)}
                className="border border-slate-200 rounded-xl px-3 py-2 text-sm w-full max-w-sm focus:outline-none focus:ring-2 focus:ring-teal-400 bg-slate-50">
                <option value="">-- Choose room type --</option>
                {roomTypes.map((rt) => <option key={rt.roomTypeId} value={rt.roomTypeId}>{rt.typeName}</option>)}
              </select>
            </div>
          )}

          <form onSubmit={handleUpsertInventory} className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <FormInput label="Start Date" type="date" required value={invForm.startDate}
              onChange={set(setInvForm, 'startDate')} min={today} />
            <FormInput label="End Date" type="date" required value={invForm.endDate}
              onChange={set(setInvForm, 'endDate')} min={invForm.startDate} />
            <FormInput label="Room Count" type="number" min="0" required value={invForm.availableCount}
              onChange={set(setInvForm, 'availableCount')} />

            <div className="flex flex-col gap-1">
              <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Availability Status</label>
              <div className="flex items-center gap-5 h-[42px]">
                <label className="flex items-center gap-2 cursor-pointer text-sm font-medium text-slate-700">
                  <input type="radio" checked={invForm.isAvailable === false}
                    onChange={() => setInvForm((f) => ({ ...f, isAvailable: false }))} />
                  Occupied
                </label>
                <label className="flex items-center gap-2 cursor-pointer text-sm font-medium text-slate-700">
                  <input type="radio" checked={invForm.isAvailable === true}
                    onChange={() => setInvForm((f) => ({ ...f, isAvailable: true }))} />
                  Vacant
                </label>
              </div>
            </div>

            <div className="sm:col-span-2">
              <button type="submit" className="bg-teal-600 text-white px-5 py-2 rounded-xl font-semibold hover:bg-teal-700 transition text-sm shadow-sm">
                Update Inventory
              </button>
            </div>
          </form>
        </div>
      )}
    </div>
  )
}

function FormInput({ label, ...props }) {
  return (
    <div className="flex flex-col gap-1">
      <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">{label}</label>
      <input
        {...props}
        className="border border-slate-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-teal-400 bg-slate-50"
      />
    </div>
  )
}
