type SessionHeaderProps = {
  mode: string;
  skillArea: string;
  questionNumber: number;
  sessionId: string;
};

export function SessionHeader({ mode, skillArea, questionNumber, sessionId }: SessionHeaderProps) {
  return (
    <section className="panel session-summary" aria-labelledby="session-title">
      <p className="eyebrow">Session in progress</p>
      <h1 id="session-title">AI-102 Study Coach</h1>
      <div className="session-grid">
        <p>
          <span className="meta-label">Mode</span>
          {mode}
        </p>
        <p>
          <span className="meta-label">Focus area</span>
          {skillArea}
        </p>
        <p>
          <span className="meta-label">Question</span>
          {Math.max(questionNumber, 1)}
        </p>
        {sessionId && (
          <p>
            <span className="meta-label">Session</span>
            {sessionId}
          </p>
        )}
      </div>
    </section>
  );
}
