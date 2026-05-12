import axios from 'axios'
import { supabase } from '../lib/supabase'

const BASE_URL = import.meta.env.VITE_GATEWAY_URL || 'http://localhost:5158'

const client = axios.create({ baseURL: `${BASE_URL}/gateway/v1` })

client.interceptors.request.use(async (config) => {
  const { data: { session } } = await supabase.auth.getSession()
  if (session?.access_token) {
    config.headers.Authorization = `Bearer ${session.access_token}`
  }
  return config
})

export default client
