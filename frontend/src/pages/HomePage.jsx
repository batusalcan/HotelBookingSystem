import { useState } from 'react'
import { useNavigate } from 'react-router-dom'

const DESTINATIONS = [
  { name: 'Istanbul', img: 'https://images.unsplash.com/photo-1524231757912-21f4fe3a7200?auto=format&fit=crop&w=800&q=80' },
  { name: 'Bodrum',   img: 'https://images.unsplash.com/photo-1547036967-23d11aacaee0?auto=format&fit=crop&w=800&q=80' },
  { name: 'Antalya',  img: 'https://images.unsplash.com/photo-1570168007204-dfb528c6958f?auto=format&fit=crop&w=800&q=80' },
  { name: 'Izmir',    img: 'https://images.unsplash.com/photo-1600585154526-990dced4db0d?auto=format&fit=crop&w=800&q=80' },
]

const FEATURES = [
  { icon: '🏆', title: 'Best Price Guarantee', desc: 'Find a lower price? We\'ll match it.' },
  { icon: '✅', title: 'Free Cancellation', desc: 'Flexible on most bookings.' },
  { icon: '⭐', title: 'Verified Reviews', desc: 'Real ratings from real guests.' },
  { icon: '🔒', title: 'Secure Booking', desc: 'Your data is always protected.' },
]

export default function HomePage() {
  const navigate = useNavigate()
  const today = new Date().toISOString().split('T')[0]
  const nextWeek = new Date(Date.now() + 7 * 86400000).toISOString().split('T')[0]

  const [form, setForm] = useState({
    destination: '',
    startDate: today,
    endDate: nextWeek,
    guestCount: 2,
  })

  const handleSearch = (e) => {
    e.preventDefault()
    if (!form.destination.trim()) return
    navigate(`/search?${new URLSearchParams({
      destination: form.destination,
      startDate: form.startDate,
      endDate: form.endDate,
      guestCount: form.guestCount,
    }).toString()}`)
  }

  const set = (key) => (e) => setForm((f) => ({ ...f, [key]: e.target.value }))

  const searchDestination = (name) => {
    navigate(`/search?${new URLSearchParams({
      destination: name,
      startDate: today,
      endDate: nextWeek,
      guestCount: 2,
    }).toString()}`)
  }

  return (
    <div>
      {/* HERO */}
      <div
        className="relative min-h-[580px] flex items-center justify-center"
        style={{
          backgroundImage: 'url(https://images.unsplash.com/photo-1566073771259-6a8506099945?auto=format&fit=crop&w=1920&q=80)',
          backgroundSize: 'cover',
          backgroundPosition: 'center',
        }}
      >
        <div className="absolute inset-0 bg-gradient-to-b from-black/50 to-black/70" />

        <div className="relative z-10 w-full max-w-5xl mx-auto px-4 py-16 text-center">
          <div className="inline-block bg-teal-500/30 backdrop-blur-sm border border-teal-400/40 text-teal-200 text-xs font-semibold px-4 py-1.5 rounded-full mb-6 tracking-widest uppercase">
            Over 1,000 hotels across Turkey
          </div>

          <h1 className="text-5xl md:text-6xl font-extrabold text-white mb-4 leading-tight drop-shadow-lg">
            Find Your Perfect Stay
          </h1>
          <p className="text-xl text-white/80 mb-10 font-light">
            Discover amazing hotels at unbeatable prices — sign in and save 15% instantly.
          </p>

          {/* Search Card */}
          <form
            onSubmit={handleSearch}
            className="bg-white rounded-2xl shadow-2xl p-4 flex flex-col md:flex-row gap-3 text-slate-800"
          >
            <div className="flex-1 flex flex-col gap-1 text-left">
              <label className="text-xs font-bold text-slate-400 uppercase tracking-wide px-1">Destination</label>
              <input
                type="text"
                required
                placeholder="City, region, or hotel name"
                value={form.destination}
                onChange={set('destination')}
                className="border border-slate-200 rounded-xl px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-teal-400 text-sm bg-slate-50"
              />
            </div>

            <div className="flex gap-3">
              <div className="flex flex-col gap-1 text-left">
                <label className="text-xs font-bold text-slate-400 uppercase tracking-wide px-1">Check-in</label>
                <input
                  type="date"
                  required
                  value={form.startDate}
                  min={today}
                  onChange={set('startDate')}
                  className="border border-slate-200 rounded-xl px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-teal-400 text-sm bg-slate-50"
                />
              </div>
              <div className="flex flex-col gap-1 text-left">
                <label className="text-xs font-bold text-slate-400 uppercase tracking-wide px-1">Check-out</label>
                <input
                  type="date"
                  required
                  value={form.endDate}
                  min={form.startDate}
                  onChange={set('endDate')}
                  className="border border-slate-200 rounded-xl px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-teal-400 text-sm bg-slate-50"
                />
              </div>
            </div>

            <div className="flex flex-col gap-1 text-left">
              <label className="text-xs font-bold text-slate-400 uppercase tracking-wide px-1">Guests</label>
              <input
                type="number"
                required
                min="1"
                max="20"
                value={form.guestCount}
                onChange={set('guestCount')}
                className="border border-slate-200 rounded-xl px-3 py-2.5 w-24 focus:outline-none focus:ring-2 focus:ring-teal-400 text-sm bg-slate-50"
              />
            </div>

            <div className="flex items-end">
              <button
                type="submit"
                className="bg-teal-600 text-white px-8 py-2.5 rounded-xl font-bold hover:bg-teal-700 transition text-sm h-[46px] shadow-lg hover:shadow-teal-500/30"
              >
                Search
              </button>
            </div>
          </form>
        </div>
      </div>

      {/* FEATURES BAR */}
      <div className="bg-teal-50 border-b border-teal-100">
        <div className="max-w-5xl mx-auto px-4 py-8 grid grid-cols-2 md:grid-cols-4 gap-6">
          {FEATURES.map((f) => (
            <div key={f.title} className="flex items-start gap-3">
              <span className="text-2xl mt-0.5">{f.icon}</span>
              <div>
                <div className="font-semibold text-slate-800 text-sm">{f.title}</div>
                <div className="text-slate-500 text-xs mt-0.5">{f.desc}</div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* POPULAR DESTINATIONS */}
      <div className="bg-white py-14 px-4">
        <div className="max-w-5xl mx-auto">
          <h2 className="text-2xl font-bold text-slate-800 mb-1">Popular Destinations</h2>
          <p className="text-slate-500 text-sm mb-6">Explore top-rated hotels in Turkey's most loved cities</p>

          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {DESTINATIONS.map((d) => (
              <button
                key={d.name}
                onClick={() => searchDestination(d.name)}
                className="group relative rounded-2xl overflow-hidden h-44 shadow-sm hover:shadow-xl transition-shadow"
              >
                <img
                  src={d.img}
                  alt={d.name}
                  className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                />
                <div className="absolute inset-0 bg-gradient-to-t from-black/70 to-transparent" />
                <div className="absolute bottom-3 left-3 text-white font-bold text-lg drop-shadow">{d.name}</div>
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* MEMBER DISCOUNT BANNER */}
      <div className="bg-gradient-to-r from-teal-700 to-teal-500 text-white py-12 px-4 text-center">
        <h2 className="text-2xl font-bold mb-2">Sign in & save 15% on every booking</h2>
        <p className="text-teal-100 mb-6 text-sm">Join thousands of travelers who book smarter.</p>
        <a
          href="/login"
          className="inline-block bg-white text-teal-700 font-bold px-8 py-3 rounded-xl hover:bg-teal-50 transition shadow"
        >
          Sign In to Unlock Deals
        </a>
      </div>
    </div>
  )
}
