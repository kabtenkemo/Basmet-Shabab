import { useEffect, useState, type FormEvent } from 'react';
import { FiPlus, FiThumbsDown, FiThumbsUp } from 'react-icons/fi';
import { Button, Card, EmptyState, Field, Input, Modal, SectionTitle, Textarea } from '../components/ui';
import { getSuggestions, createSuggestion, voteSuggestion } from '../api';

interface Suggestion {
  suggestionId: string;
  title: string;
  description: string;
  createdByName: string;
  createdAtUtc: string;
  acceptCount: number;
  rejectCount: number;
  userVote: boolean | null;
}

const pageTitles = {
  suggestions: {
    eyebrow: 'Suggestions',
    title: 'مكان للمقترحات والأفكار',
    description: 'اقترح أفكارًا جديدة وصوّت على اقتراحات الآخرين لتطوير المؤسسة والعمل.'
  }
};

function formatDate(value: string) {
  return new Intl.DateTimeFormat('ar-EG', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

export function SuggestionsPage() {
  const [suggestions, setSuggestions] = useState<Array<{ suggestionId: string; title: string; description: string; createdByName: string; createdAtUtc: string; acceptCount: number; rejectCount: number; userVote: boolean | null }>>([]);
  const [loading, setLoading] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [submitError, setSubmitError] = useState('');
  const [voting, setVoting] = useState<string | null>(null);

  useEffect(() => {
    loadSuggestions();
  }, [page]);

  const loadSuggestions = async () => {
    setLoading(true);
    try {
      const data = await getSuggestions(page, pageSize) as Array<{ suggestionId: string; title: string; description: string; createdByName: string; createdAtUtc: string; acceptCount: number; rejectCount: number; userVote: boolean | null }>;
      setSuggestions(data);
    } finally {
      setLoading(false);
    }
  };

  const handleCreateSuggestion = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitError('');

    if (!title.trim() || !description.trim()) {
      setSubmitError('العنوان والوصف مطلوبان.');
      return;
    }

    try {
      await createSuggestion(title, description);
      setTitle('');
      setDescription('');
      setCreateOpen(false);
      setPage(1);
      await loadSuggestions();
    } catch (error) {
      setSubmitError(error instanceof Error ? error.message : 'فشل إنشاء الاقتراح');
    }
  };

  const handleVote = async (suggestionId: string, isAccepted: boolean) => {
    setVoting(suggestionId);
    try {
      await voteSuggestion(suggestionId, isAccepted);
      await loadSuggestions();
    } finally {
      setVoting(null);
    }
  };

  return (
    <div className="space-y-6">
      <SectionTitle
        eyebrow={pageTitles.suggestions.eyebrow}
        title={pageTitles.suggestions.title}
        description={pageTitles.suggestions.description}
        actions={
          <Button onClick={() => setCreateOpen(true)}>
            <span className="inline-flex items-center gap-2">
              <FiPlus /> اقتراح جديد
            </span>
          </Button>
        }
      />

      <Card title="الاقتراحات المقدمة" subtitle="Community suggestions">
        {loading ? (
          <div className="text-center py-12 text-slate-400">جاري التحميل...</div>
        ) : suggestions.length === 0 ? (
          <EmptyState
            title="لا توجد اقتراحات بعد"
            description="كن أول من يقدم فكرة جديدة لتطوير المؤسسة والعمل!"
          />
        ) : (
          <div className="space-y-3">
            {suggestions.map((suggestion) => (
              <div
                key={suggestion.suggestionId}
                className="rounded-3xl border border-white/10 bg-white/5 p-4 hover:bg-white/[0.08] transition-colors"
              >
                <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                  <div className="flex-1">
                    <p className="text-lg font-bold text-white">{suggestion.title}</p>
                    <p className="mt-2 text-sm leading-6 text-slate-300">{suggestion.description}</p>
                    <div className="mt-3 flex flex-wrap gap-3 text-xs text-slate-400">
                      <span>بواسطة: {suggestion.createdByName}</span>
                      <span>·</span>
                      <span>{formatDate(suggestion.createdAtUtc)}</span>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    <Button
                      variant={suggestion.userVote === true ? 'primary' : 'secondary'}
                      disabled={voting === suggestion.suggestionId}
                      onClick={() => handleVote(suggestion.suggestionId, true)}
                    >
                      <span className="inline-flex items-center gap-1">
                        <FiThumbsUp /> {suggestion.acceptCount}
                      </span>
                    </Button>
                    <Button
                      variant={suggestion.userVote === false ? 'danger' : 'secondary'}
                      disabled={voting === suggestion.suggestionId}
                      onClick={() => handleVote(suggestion.suggestionId, false)}
                    >
                      <span className="inline-flex items-center gap-1">
                        <FiThumbsDown /> {suggestion.rejectCount}
                      </span>
                    </Button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>

      <Modal
        open={createOpen}
        onClose={() => {
          setCreateOpen(false);
          setSubmitError('');
        }}
        title="اقتراح فكرة جديدة"
        subtitle="New suggestion"
        footer={
          <>
            <Button
              variant="ghost"
              onClick={() => {
                setCreateOpen(false);
                setSubmitError('');
              }}
            >
              إلغاء
            </Button>
            <Button type="submit" form="suggestion-form">
              <span className="inline-flex items-center gap-2">
                <FiPlus /> إرسال الاقتراح
              </span>
            </Button>
          </>
        }
      >
        <form id="suggestion-form" className="space-y-4" onSubmit={handleCreateSuggestion}>
          <Field label="العنوان" hint="اكتب عنوانًا موجزًا للاقتراح (3-200 حرف)">
            <Input
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="مثال: إضافة ميزة التقارير الشهرية"
              maxLength={200}
            />
          </Field>
          <Field label="الوصف التفصيلي" hint="اشرح الفكرة بالتفصيل (10-1000 حرف)">
            <Textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="اشرح كيفية ستساهم هذه الفكرة في تطوير المؤسسة..."
              rows={5}
              maxLength={1000}
            />
          </Field>
          {submitError && (
            <div className="rounded-2xl border border-rose-400/20 bg-rose-400/10 px-4 py-3 text-sm text-rose-200">
              {submitError}
            </div>
          )}
        </form>
      </Modal>
    </div>
  );
}
