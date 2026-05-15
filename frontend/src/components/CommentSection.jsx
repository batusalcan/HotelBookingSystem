import { useState, useEffect } from 'react'
import { getComments, postComment } from '../api/commentsApi'
import { useAuth } from '../context/AuthContext'

const CATEGORIES = [
  { key: 'cleanliness',       label: 'Cleanliness' },
  { key: 'staff',             label: 'Staff & Service' },
  { key: 'facilities',        label: 'Facilities' },
  { key: 'locationCondition', label: 'Location' },
  { key: 'ecoFriendly',       label: 'Eco-friendly' },
]

function RatingBar({ label, value }) {
  const pct = ((value ?? 0) / 10) * 100
  return (
    <div className="flex items-center gap-3 text-sm">
      <span className="w-36 text-slate-500 text-right shrink-0 text-xs">{label}</span>
      <div className="flex-1 bg-slate-100 rounded-full h-2">
        <div className="bg-teal-500 h-2 rounded-full transition-all" style={{ width: `${pct}%` }} />
      </div>
      <span className="w-12 text-slate-700 font-bold text-xs">{value?.toFixed(1)}/10</span>
    </div>
  )
}

export default function CommentSection({ hotelId }) {
  const { session } = useAuth()
  const [data, setData] = useState(null)
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState({
    rating: '',
    text: '',
    tripType: '',
    categoryRatings: { cleanliness: '', staff: '', facilities: '', locationCondition: '', ecoFriendly: '' },
  })
  const [submitting, setSubmitting] = useState(false)
  const [submitMsg, setSubmitMsg] = useState('')

  const load = async (p) => {
    setLoading(true)
    try {
      const res = await getComments(hotelId, { page: p, pageSize: 5 })
      setData(res.data.data)
    } catch {
      setData(null)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load(page) }, [hotelId, page])

  const handleSubmit = async (e) => {
    e.preventDefault()
    setSubmitting(true)
    setSubmitMsg('')
    try {
      await postComment(hotelId, {
        rating: parseFloat(form.rating),
        text: form.text,
        tripType: form.tripType,
        categoryRatings: Object.fromEntries(
          Object.entries(form.categoryRatings).map(([k, v]) => [k, parseFloat(v)])
        ),
      })
      setSubmitMsg('Your review was submitted!')
      setShowForm(false)
      load(1)
    } catch {
      setSubmitMsg('Submission failed. Please try again.')
    } finally {
      setSubmitting(false)
    }
  }

  if (loading) return <p className="text-slate-400 py-6 text-sm">Loading reviews...</p>
  if (!data)   return <p className="text-slate-400 py-6 text-sm">Reviews could not be loaded.</p>

  return (
    <div className="mt-6">
      <h2 className="text-xl font-bold text-slate-800 mb-4">Guest Reviews</h2>

      <div className="bg-white rounded-2xl border border-slate-100 shadow-sm p-6">
        {/* Score overview */}
        <div className="flex items-start gap-8 mb-6 flex-wrap">
          <div className="text-center">
            <div className="text-4xl font-extrabold text-teal-700">{data.overallScore?.toFixed(1)}</div>
            <div className="text-xs text-slate-400 mt-1">out of 10</div>
            <div className="text-xs text-slate-500 mt-0.5">{data.totalReviews} verified reviews</div>
          </div>
          <div className="flex-1 flex flex-col gap-2 min-w-[240px]">
            {CATEGORIES.map((c) => (
              <RatingBar key={c.key} label={c.label} value={data.categoryBreakdown?.[c.key]} />
            ))}
          </div>
        </div>

        {/* Write review toggle */}
        {session && (
          <div className="mb-5">
            <button
              onClick={() => setShowForm((s) => !s)}
              className="text-sm text-teal-600 font-semibold hover:underline"
            >
              {showForm ? 'Cancel' : '+ Write a Review'}
            </button>
          </div>
        )}

        {/* Review form */}
        {showForm && (
          <form onSubmit={handleSubmit} className="mb-6 bg-slate-50 rounded-2xl p-5 border border-slate-100 flex flex-col gap-3">
            <div className="flex gap-4 flex-wrap">
              <div className="flex flex-col gap-1">
                <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Overall Score (1–10)</label>
                <input type="number" min="1" max="10" step="0.1" required value={form.rating}
                  onChange={(e) => setForm((f) => ({ ...f, rating: e.target.value }))}
                  className="border border-slate-200 rounded-xl px-3 py-2 w-24 text-sm focus:outline-none focus:ring-2 focus:ring-teal-400 bg-white" />
              </div>
              <div className="flex flex-col gap-1">
                <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Trip Type</label>
                <input type="text" placeholder="e.g. 4-night stay" value={form.tripType}
                  onChange={(e) => setForm((f) => ({ ...f, tripType: e.target.value }))}
                  className="border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-teal-400 bg-white" />
              </div>
            </div>
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
              {CATEGORIES.map((c) => (
                <div key={c.key} className="flex flex-col gap-1">
                  <label className="text-xs text-slate-500">{c.label}</label>
                  <input type="number" min="1" max="10" step="0.1" required
                    value={form.categoryRatings[c.key]}
                    onChange={(e) => setForm((f) => ({ ...f, categoryRatings: { ...f.categoryRatings, [c.key]: e.target.value } }))}
                    className="border border-slate-200 rounded-xl px-3 py-2 w-full text-sm focus:outline-none focus:ring-2 focus:ring-teal-400 bg-white" />
                </div>
              ))}
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Your Review</label>
              <textarea required value={form.text} rows={3}
                onChange={(e) => setForm((f) => ({ ...f, text: e.target.value }))}
                className="border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-teal-400 bg-white resize-none" />
            </div>
            <div className="flex items-center gap-3">
              <button type="submit" disabled={submitting}
                className="bg-teal-600 text-white px-5 py-2 rounded-xl text-sm font-bold hover:bg-teal-700 disabled:opacity-50 transition">
                Submit Review
              </button>
              {submitMsg && <span className="text-sm text-teal-600 font-medium">{submitMsg}</span>}
            </div>
          </form>
        )}

        {/* Comments list */}
        <div className="flex flex-col divide-y divide-slate-100">
          {data.comments?.map((c, i) => (
            <div key={i} className="py-4">
              <div className="flex items-center gap-3 mb-2">
                <span className="bg-teal-600 text-white text-xs px-2 py-0.5 rounded font-bold">{c.rating}/10</span>
                <span className="font-semibold text-slate-800 text-sm">{c.author}</span>
                {c.tripType && <span className="text-slate-400 text-xs">· {c.tripType}</span>}
                <span className="text-slate-300 text-xs ml-auto">
                  {c.date ? new Date(c.date).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' }) : ''}
                </span>
              </div>
              <p className="text-slate-600 text-sm leading-relaxed">{c.text}</p>
            </div>
          ))}
        </div>

        {/* Pagination */}
        {data.totalPages > 1 && (
          <div className="flex gap-2 mt-5 justify-center">
            {Array.from({ length: data.totalPages }, (_, i) => i + 1).map((p) => (
              <button key={p} onClick={() => setPage(p)}
                className={`w-9 h-9 rounded-full text-sm font-bold transition ${
                  p === page ? 'bg-teal-600 text-white shadow' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                }`}>
                {p}
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
