import { PieChart, Pie, Cell, Tooltip, ResponsiveContainer, Legend } from 'recharts';

const COLORS = ['#6366f1', '#10b981', '#f59e0b', '#ec4899', '#3b82f6', '#a855f7'];

export default function DeviceChart({ devices, loading }) {
  if (loading) return <div className="skeleton" style={{ height: 200 }} />;

  const data = devices?.map(d => ({
    name: d.device === 'desktop' ? '🖥 Masaüstü'
        : d.device === 'mobile'  ? '📱 Mobil'
        : d.device === 'tablet'  ? '📟 Tablet'
        : d.device,
    value: d.count,
  })) ?? [];

  if (!data.length) return (
    <div style={{ textAlign: 'center', color: 'var(--text-muted)', padding: '2rem', fontSize: 14 }}>
      Veri yok
    </div>
  );

  const total = data.reduce((s, d) => s + d.value, 0);

  return (
    <ResponsiveContainer width="100%" height={200}>
      <PieChart>
        <Pie
          data={data}
          cx="50%"
          cy="50%"
          innerRadius={55}
          outerRadius={80}
          paddingAngle={3}
          dataKey="value"
        >
          {data.map((_, i) => (
            <Cell key={i} fill={COLORS[i % COLORS.length]} />
          ))}
        </Pie>
        <Tooltip
          formatter={(value) => [`${value.toLocaleString()} (${((value/total)*100).toFixed(1)}%)`, '']}
          contentStyle={{
            background: 'var(--bg-elevated)',
            border: '1px solid var(--border)',
            borderRadius: 10,
            fontSize: 13,
          }}
        />
        <Legend
          iconType="circle"
          iconSize={8}
          formatter={(v) => <span style={{ color: 'var(--text-secondary)', fontSize: 12 }}>{v}</span>}
        />
      </PieChart>
    </ResponsiveContainer>
  );
}
