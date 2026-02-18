import {useEffect, useState} from 'react'
import './App.css'
import {StateleSSEClient} from "statele-sse";
import {type Measurement, WebClientClient} from "./generated-ts-client.ts";
import {LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer} from 'recharts'

const sse = new StateleSSEClient("http://localhost:5233/sse")
const restClient = new WebClientClient("http://localhost:5233")

const metrics = [
  {key: 'temperature' as const, label: 'Temperature (°C)', color: '#ef4444'},
  {key: 'humidity' as const, label: 'Humidity (%)', color: '#3b82f6'},
  {key: 'pressure' as const, label: 'Pressure (hPa)', color: '#22c55e'},
  {key: 'lightLevel' as const, label: 'Light Level', color: '#f59e0b'},
] as const

function App() {
  const [measurements, setMeasurements] = useState<Measurement[]>([])

  useEffect(() => {
    sse.listen(async (id) => {
      const result = await restClient.getMeasurements(id);
      return result;
    }, (data) => {
      setMeasurements(data);
    })
  }, []);

  const chartData = measurements.map(m => ({
    ...m,
    time: m.timestamp ? new Date(m.timestamp).toLocaleTimeString() : '',
  }))

  return (
    <div style={{display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, padding: 16}}>
      {metrics.map(({key, label, color}) => (
        <div key={key}>
          <h3>{label}</h3>
          <ResponsiveContainer width="100%" height={250}>
            <LineChart data={chartData}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="time" tick={{fontSize: 10}} />
              <YAxis />
              <Tooltip />
              <Line type="monotone" dataKey={key} stroke={color} dot={false} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      ))}
    </div>
  )
}

export default App
