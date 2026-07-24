import {
  XAxis, YAxis, CartesianGrid,
  Tooltip, ResponsiveContainer, Area, AreaChart
} from 'recharts';
import { useTheme } from '../context/ThemeContext';

const CustomTooltip = ({ active, payload, label }) => {
  if (!active || !payload?.length) return null;
  return (
    <div style={{
      background: 'var(--bg-elevated)',
      border: '1px solid var(--border)',
      borderRadius: 10,
      padding: '10px 14px',
      fontSize: 13,
      boxShadow: 'var(--shadow-md)',
    }}>
      <div style={{ color: 'var(--text-muted)', marginBottom: 6, fontSize: 12 }}>{label}</div>
      {payload.map(p => (
        <div key={p.dataKey} style={{ color: p.color, fontWeight: 600 }}>
          {p.name}: {p.value.toLocaleString()}
        </div>
      ))}
    </div>
  );
};

export default function TrafficChart({ data, loading }) {
  const { theme } = useTheme();
  const gridColor  = theme === 'dark' ? 'rgba(255,255,255,0.05)' : 'rgba(0,0,0,0.06)';
  const axisColor  = theme === 'dark' ? '#4a5068' : '#9ba3bf';

  if (loading) {
    return (
      <div style={{ height: 280 }} className="skeleton" />
    );
  }

  if (!data?.length) {
    return (
      <div style={{
        height: 280, display: 'flex', alignItems: 'center',
        justifyContent: 'center', color: 'var(--text-muted)',
        fontSize: 14, flexDirection: 'column', gap: 8
      }}>
        <span style={{ fontSize: 32 }}>📊</span>
        <span>Henüz veri yok</span>
      </div>
    );
  }

  return (
    <ResponsiveContainer width="100%" height={280}>
      <AreaChart data={data} margin={{ top: 10, right: 10, left: -10, bottom: 0 }}>
        <defs>
          <linearGradient id="gradPV" x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%"  stopColor="#6366f1" stopOpacity={0.3} />
            <stop offset="95%" stopColor="#6366f1" stopOpacity={0} />
          </linearGradient>
          <linearGradient id="gradUV" x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%"  stopColor="#10b981" stopOpacity={0.3} />
            <stop offset="95%" stopColor="#10b981" stopOpacity={0} />
          </linearGradient>
        </defs>
        <CartesianGrid stroke={gridColor} strokeDasharray="3 3" />
        <XAxis
          dataKey="date"
          tick={{ fill: axisColor, fontSize: 11 }}
          tickLine={false}
          axisLine={false}
          tickFormatter={d => d.slice(5)} // "2024-01-15" → "01-15"
        />
        <YAxis
          tick={{ fill: axisColor, fontSize: 11 }}
          tickLine={false}
          axisLine={false}
          tickFormatter={v => v >= 1000 ? `${(v/1000).toFixed(1)}k` : v}
        />
        <Tooltip content={<CustomTooltip />} />
        <Area
          type="monotone"
          dataKey="pageviews"
          name="Sayfa Görüntüleme"
          stroke="#6366f1"
          strokeWidth={2}
          fill="url(#gradPV)"
          dot={false}
          activeDot={{ r: 5, fill: '#6366f1' }}
        />
        <Area
          type="monotone"
          dataKey="unique_visitors"
          name="Tekil Ziyaretçi"
          stroke="#10b981"
          strokeWidth={2}
          fill="url(#gradUV)"
          dot={false}
          activeDot={{ r: 5, fill: '#10b981' }}
        />
      </AreaChart>
    </ResponsiveContainer>
  );
}
