type StartPageProps = {
  onBegin: () => void;
};

export function StartPage({ onBegin }: StartPageProps) {
  return (
    <section className="panel hero-panel" aria-labelledby="start-title">
      <p className="eyebrow">AI-102 exam prep</p>
      <h1 id="start-title">AI-102 Study Coach</h1>
      <p className="lead">
        Build exam confidence with a guided setup and question-by-question feedback rooted in verified Microsoft Learn
        sources.
      </p>
      <button className="button-primary" onClick={onBegin}>
        Begin
      </button>
    </section>
  );
}
