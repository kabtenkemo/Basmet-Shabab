import type { ReactNode } from 'react';

export function Card({
  title,
  subtitle,
  actions,
  children,
  className = ''
}: {
  title?: string;
  subtitle?: string;
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  return (
    <section className={`rounded-3xl border border-white/10 bg-slate-900/80 p-4 shadow-glow backdrop-blur sm:p-5 ${className}`}>
      {(title || subtitle || actions) && (
        <header className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div>
            {subtitle && <p className="text-xs font-semibold uppercase tracking-[0.35em] text-brand-300/80">{subtitle}</p>}
            {title && <h2 className="mt-2 text-lg font-bold text-white sm:text-xl">{title}</h2>}
          </div>
          {actions}
        </header>
      )}
      {children}
    </section>
  );
}

export function StatCard({ label, value, hint, accent = 'brand' }: { label: string; value: string | number; hint?: string; accent?: 'brand' | 'amber' | 'sky' | 'rose' | 'success' }) {
  const palette: Record<string, string> = {
    brand: 'from-brand-400/25 to-brand-600/5 text-brand-100 border-brand-400/15',
    amber: 'from-amber-400/25 to-amber-600/5 text-amber-100 border-amber-400/15',
    sky: 'from-sky-400/25 to-sky-600/5 text-sky-100 border-sky-400/15',
    rose: 'from-rose-400/25 to-rose-600/5 text-rose-100 border-rose-400/15',
    success: 'from-emerald-400/25 to-emerald-600/5 text-emerald-100 border-emerald-400/15'
  };

  return (
    <article className={`rounded-3xl border bg-gradient-to-br p-4 shadow-glow sm:p-5 ${palette[accent]}`}>
      <p className="text-sm font-semibold text-white/70">{label}</p>
      <p className="mt-3 text-2xl font-extrabold text-white sm:text-3xl">{value}</p>
      {hint && <p className="mt-2 text-sm text-white/70">{hint}</p>}
    </article>
  );
}

export function Badge({ children, tone = 'brand' }: { children: ReactNode; tone?: 'brand' | 'success' | 'warning' | 'danger' | 'neutral' }) {
  const classes: Record<string, string> = {
    brand: 'bg-brand-400/15 text-brand-200 ring-brand-400/30',
    success: 'bg-emerald-400/15 text-emerald-200 ring-emerald-400/30',
    warning: 'bg-amber-400/15 text-amber-200 ring-amber-400/30',
    danger: 'bg-rose-400/15 text-rose-200 ring-rose-400/30',
    neutral: 'bg-slate-400/15 text-slate-200 ring-slate-400/30'
  };

  return <span className={`inline-flex items-center rounded-full px-3 py-1 text-xs font-bold ring-1 ${classes[tone]}`}>{children}</span>;
}

export function Modal({
  open,
  title,
  subtitle,
  onClose,
  children,
  footer
}: {
  open: boolean;
  title: string;
  subtitle?: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
}) {
  if (!open) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 px-4 py-6 backdrop-blur-sm" role="dialog" aria-modal="true">
      <div className="w-full max-w-3xl max-h-[calc(100vh-2rem)] overflow-y-auto rounded-[2rem] border border-white/10 bg-slate-900 p-5 shadow-2xl shadow-black/40 scrollbar-thin sm:max-h-[calc(100vh-4rem)] sm:p-6">
        <div className="mb-5 flex items-start justify-between gap-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.35em] text-brand-300/75">{subtitle}</p>
            <h3 className="mt-2 text-xl font-bold text-white sm:text-2xl">{title}</h3>
          </div>
          <button type="button" onClick={onClose} className="rounded-full border border-white/10 px-3 py-2 text-sm text-slate-300 transition hover:bg-white/5">
            إغلاق
          </button>
        </div>
        <div>{children}</div>
        {footer && <div className="mt-6 flex flex-wrap items-center justify-end gap-3">{footer}</div>}
      </div>
    </div>
  );
}

export function Field({ label, children, hint, className = '' }: { label: string; children: ReactNode; hint?: string; className?: string }) {
  return (
    <label className={`block ${className}`}>
      <span className="mb-2 block text-sm font-semibold text-slate-200">{label}</span>
      {children}
      {hint && <span className="mt-2 block text-xs text-slate-400">{hint}</span>}
    </label>
  );
}

export function Input(props: React.InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      {...props}
      className={`w-full rounded-2xl border border-white/10 bg-slate-950/70 px-4 py-3 text-slate-100 outline-none transition placeholder:text-slate-500 focus:border-brand-400/40 focus:ring-2 focus:ring-brand-400/20 ${props.className ?? ''}`}
    />
  );
}

export function Textarea(props: React.TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <textarea
      {...props}
      className={`w-full rounded-2xl border border-white/10 bg-slate-950/70 px-4 py-3 text-slate-100 outline-none transition placeholder:text-slate-500 focus:border-brand-400/40 focus:ring-2 focus:ring-brand-400/20 ${props.className ?? ''}`}
    />
  );
}

export function Select(props: React.SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select
      {...props}
      className={`w-full rounded-2xl border border-white/10 bg-slate-950/70 px-4 py-3 text-slate-100 outline-none transition focus:border-brand-400/40 focus:ring-2 focus:ring-brand-400/20 ${props.className ?? ''}`}
    />
  );
}

export function Button({ variant = 'primary', className = '', ...props }: React.ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'secondary' | 'ghost' | 'danger' }) {
  const styles: Record<string, string> = {
    primary: 'bg-brand-500 text-slate-950 hover:bg-brand-400',
    secondary: 'bg-white/5 text-slate-100 hover:bg-white/10',
    ghost: 'border border-white/10 bg-transparent text-slate-200 hover:bg-white/5',
    danger: 'bg-rose-500 text-white hover:bg-rose-400'
  };

  return <button {...props} type={props.type ?? 'button'} className={`rounded-2xl px-4 py-3 text-sm font-bold transition ${styles[variant]} ${className}`} />;
}

export function SectionTitle({ eyebrow, title, description, actions }: { eyebrow: string; title: string; description?: string; actions?: ReactNode }) {
  return (
    <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
      <div>
        <p className="text-xs font-semibold uppercase tracking-[0.35em] text-brand-300/75">{eyebrow}</p>
        <h2 className="mt-2 text-xl font-extrabold text-white sm:text-2xl">{title}</h2>
        {description && <p className="mt-2 max-w-3xl text-sm leading-7 text-slate-300">{description}</p>}
      </div>
      {actions}
    </div>
  );
}

export function EmptyState({ title, description }: { title: string; description: string }) {
  return (
    <div className="rounded-3xl border border-dashed border-white/10 bg-white/5 p-8 text-center text-slate-300">
      <p className="text-lg font-bold text-white">{title}</p>
      <p className="mt-2 text-sm leading-7">{description}</p>
    </div>
  );
}

export type TableColumn<T> = {
  header: string;
  render: (row: T) => ReactNode;
  className?: string;
};

export function PagedTable<T>({
  rows,
  columns,
  rowKey,
  page,
  pageSize,
  onPageChange,
  search,
  onSearchChange,
  searchPlaceholder = 'ابحث... ',
  emptyTitle,
  emptyDescription,
  className = ''
}: {
  rows: T[];
  columns: TableColumn<T>[];
  rowKey: (row: T) => string;
  page: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  search?: string;
  onSearchChange?: (value: string) => void;
  searchPlaceholder?: string;
  emptyTitle: string;
  emptyDescription: string;
  className?: string;
}) {
  const start = (page - 1) * pageSize;
  const pageRows = rows.slice(start, start + pageSize);
  const totalPages = Math.max(1, Math.ceil(rows.length / pageSize));

  return (
    <div className={`space-y-4 ${className}`}>
      {(onSearchChange || search !== undefined) && (
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          {onSearchChange ? (
            <div className="max-w-md flex-1">
              <Input value={search ?? ''} onChange={(event) => onSearchChange(event.target.value)} placeholder={searchPlaceholder} />
            </div>
          ) : <span />}
          <span className="text-sm text-slate-400">{rows.length} نتيجة</span>
        </div>
      )}

      {rows.length === 0 ? (
        <EmptyState title={emptyTitle} description={emptyDescription} />
      ) : (
        <div className="overflow-hidden rounded-3xl border border-white/10 bg-slate-950/60">
          <div className="overflow-x-auto scrollbar-thin">
            <table className="min-w-full text-right text-sm">
              <thead className="bg-white/5 text-slate-300">
                <tr>
                  {columns.map((column) => (
                    <th key={column.header} className={`px-4 py-4 font-semibold ${column.className ?? ''}`}>
                      {column.header}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {pageRows.map((row, index) => (
                  <tr key={rowKey(row)} className={index % 2 === 0 ? 'bg-white/[0.02]' : 'bg-white/[0.04]'}>
                    {columns.map((column) => (
                      <td key={column.header} className={`px-4 py-4 align-top text-slate-200 ${column.className ?? ''}`}>
                        {column.render(row)}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex flex-col gap-3 border-t border-white/10 px-4 py-4 sm:flex-row sm:items-center sm:justify-between">
            <p className="text-sm text-slate-400">الصفحة {page} من {totalPages}</p>
            <div className="flex gap-2">
              <Button variant="secondary" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>السابق</Button>
              <Button variant="secondary" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>التالي</Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
