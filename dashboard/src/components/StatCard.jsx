import { TrendingUp, TrendingDown, Minus } from 'lucide-react';
import './StatCard.css';

export default function StatCard({ icon: Icon, label, value, sub, trend, color = 'accent', loading }) {
  if (loading) {
    return (
      <div className="stat-card">
        <div className="skeleton" style={{ height: 20, width: '60%', marginBottom: 12 }} />
        <div className="skeleton" style={{ height: 36, width: '40%', marginBottom: 8 }} />
        <div className="skeleton" style={{ height: 16, width: '80%' }} />
      </div>
    );
  }

  const TrendIcon = trend > 0 ? TrendingUp : trend < 0 ? TrendingDown : Minus;
  const trendClass = trend > 0 ? 'trend--up' : trend < 0 ? 'trend--down' : 'trend--flat';

  return (
    <div className={`stat-card stat-card--${color}`}>
      <div className="stat-header">
        <span className="stat-label">{label}</span>
        {Icon && (
          <div className={`stat-icon stat-icon--${color}`}>
            <Icon size={16} />
          </div>
        )}
      </div>
      <div className="stat-value">{value ?? '—'}</div>
      {(sub !== undefined || trend !== undefined) && (
        <div className="stat-footer">
          {sub && <span className="stat-sub">{sub}</span>}
          {trend !== undefined && (
            <span className={`stat-trend ${trendClass}`}>
              <TrendIcon size={12} />
              {Math.abs(trend)}%
            </span>
          )}
        </div>
      )}
    </div>
  );
}
