import { useEffect, useState } from "react";
import { bootstrapSession, configureSession, getNextQuestion, submitAnswer } from "./api";
import type { Citation, QuizAnswerResponse, QuizQuestionResponse } from "./types";

type RoutePath = "/" | "/start" | "/session";
type OnboardingStatus = "idle" | "loading" | "awaiting_area" | "awaiting_mode" | "configuring";
type SessionStatus = "idle" | "loading_question" | "answering";

type ConversationEntry = {
  id: string;
  role: "assistant" | "user";
  kind: "info" | "question" | "feedback" | "selection";
  text: string;
  citations?: Citation[];
  verified?: boolean;
  warning?: string;
  correct?: boolean;
  memoryRule?: string;
  questionId?: string;
};

function createId() {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

function formatCitation(citation: Citation) {
  return `${citation.title} (${citation.retrievedAt})`;
}

function withChoiceLabel(choice: string, index: number) {
  const labels = ["A", "B", "C"];
  const label = labels[index] ?? String.fromCharCode(65 + index);

  if (/^[A-C]\)/i.test(choice.trim())) {
    return choice;
  }

  return `${label}) ${choice}`;
}

function normalizeRoute(pathname: string): RoutePath {
  if (pathname === "/start") {
    return "/start";
  }

  if (pathname === "/session") {
    return "/session";
  }

  return "/";
}

export function App() {
  const [route, setRoute] = useState<RoutePath>(() => normalizeRoute(window.location.pathname));
  const [sessionId, setSessionId] = useState<string>("");
  const [selectedArea, setSelectedArea] = useState<string>("");
  const [selectedMode, setSelectedMode] = useState<string>("");
  const [areaOptions, setAreaOptions] = useState<string[]>([]);
  const [modeOptions, setModeOptions] = useState<string[]>([]);
  const [onboardingMessage, setOnboardingMessage] = useState<string>("Choose your AI-102 focus area to begin.");
  const [onboardingStatus, setOnboardingStatus] = useState<OnboardingStatus>("idle");
  const [conversation, setConversation] = useState<ConversationEntry[]>([]);
  const [activeQuestion, setActiveQuestion] = useState<QuizQuestionResponse | null>(null);
  const [sessionStatus, setSessionStatus] = useState<SessionStatus>("idle");
  const [error, setError] = useState<string>("");

  useEffect(() => {
    function onPopState() {
      setRoute(normalizeRoute(window.location.pathname));
    }

    window.addEventListener("popstate", onPopState);
    return () => {
      window.removeEventListener("popstate", onPopState);
    };
  }, []);

  function navigate(nextRoute: RoutePath, replace = false) {
    if (route === nextRoute) {
      return;
    }

    if (replace) {
      window.history.replaceState(null, "", nextRoute);
    } else {
      window.history.pushState(null, "", nextRoute);
    }

    setRoute(nextRoute);
  }

  useEffect(() => {
    if (route !== "/start" || onboardingStatus !== "idle") {
      return;
    }

    async function initializeOnboarding() {
      try {
        setError("");
        setOnboardingStatus("loading");
        setActiveQuestion(null);
        setConversation([]);
        setSelectedArea("");
        setSelectedMode("");

        const bootstrap = await bootstrapSession();
        setSessionId(bootstrap.sessionId);
        setAreaOptions(bootstrap.areaOptions);
        setModeOptions(bootstrap.modeOptions);
        setOnboardingMessage(bootstrap.message);
        setOnboardingStatus("awaiting_area");
      } catch (err) {
        setError((err as Error).message);
        setOnboardingStatus("idle");
      }
    }

    void initializeOnboarding();
  }, [route, onboardingStatus]);

  useEffect(() => {
    if (route === "/session" && (!sessionId || !selectedArea || !selectedMode)) {
      navigate("/start", true);
    }
  }, [route, sessionId, selectedArea, selectedMode]);

  async function loadNextQuestion(currentSessionId: string) {
    setSessionStatus("loading_question");
    try {
      const question = await getNextQuestion(currentSessionId);
      const hasCitations = (question.citations?.length ?? 0) > 0;
      const hasChoices = (question.choices?.length ?? 0) > 0;

      setConversation((prev) => [
        ...prev,
        {
          id: createId(),
          role: "assistant",
          kind: "question",
          text: question.question,
          citations: question.citations,
          verified: hasCitations,
          warning: hasCitations ? undefined : "I can't answer this from verified Microsoft Learn MCP sources right now.",
          questionId: question.questionId
        }
      ]);

      setActiveQuestion(hasChoices ? question : null);
    } finally {
      setSessionStatus("idle");
    }
  }

  async function onSelectArea(option: string) {
    if (onboardingStatus !== "awaiting_area") {
      return;
    }

    setSelectedArea(option);
    setOnboardingStatus("awaiting_mode");
  }

  async function onSelectMode(mode: string) {
    if (onboardingStatus !== "awaiting_mode" || !selectedArea || !sessionId) {
      return;
    }

    setSelectedMode(mode);
    setOnboardingStatus("configuring");

    try {
      setError("");
      await configureSession(sessionId, mode, selectedArea);

      setConversation([
        {
          id: createId(),
          role: "assistant",
          kind: "info",
          text: `Session started in ${mode} mode for ${selectedArea}.`
        }
      ]);

      navigate("/session");
      setOnboardingStatus("idle");
      await loadNextQuestion(sessionId);
    } catch (err) {
      setError((err as Error).message);
      setOnboardingStatus("awaiting_mode");
    }
  }

  async function onAnswer(choice: string) {
    if (!sessionId || !activeQuestion || route !== "/session" || sessionStatus === "answering") {
      return;
    }

    try {
      setSessionStatus("answering");
      setError("");
      const feedback: QuizAnswerResponse = await submitAnswer(sessionId, activeQuestion.questionId, choice);
      const hasCitations = feedback.citations.length > 0;

      setConversation((prev) => [
        ...prev,
        {
          id: createId(),
          role: "user",
          kind: "selection",
          text: choice
        },
        {
          id: createId(),
          role: "assistant",
          kind: "feedback",
          text: feedback.explanation,
          citations: feedback.citations,
          verified: hasCitations,
          warning: hasCitations ? undefined : "I can't answer this from verified Microsoft Learn MCP sources right now.",
          correct: feedback.correct,
          memoryRule: feedback.memoryRule
        }
      ]);

      await loadNextQuestion(sessionId);
    } catch (err) {
      setError((err as Error).message);
      setSessionStatus("idle");
    }
  }

  function renderStartPage() {
    return (
      <section className="panel hero-panel">
        <p className="eyebrow">AI-102 Learning Flow</p>
        <h1>Study with a guided start, then learn in focused quiz chat</h1>
        <p className="lead">
          This coach verifies answers against Microsoft Learn citations and helps you build durable memory rules while you practice.
        </p>

        <div className="step-grid" aria-label="Learning steps">
          <article className="step-card">
            <p className="step-number">Step 1</p>
            <h2>Read the setup</h2>
            <p>Understand how the flow works and what you will choose before your first question appears.</p>
          </article>
          <article className="step-card">
            <p className="step-number">Step 2</p>
            <h2>Answer start questions</h2>
            <p>Pick your AI-102 skill area first, then select the study mode that fits your session goal.</p>
          </article>
          <article className="step-card">
            <p className="step-number">Step 3</p>
            <h2>Begin learning</h2>
            <p>Work through questions, get instant feedback, and use verified citations for reinforcement.</p>
          </article>
        </div>

        <button className="cta-button" onClick={() => navigate("/start")}>Start learning</button>
      </section>
    );
  }

  function renderOnboardingPage() {
    const isBusy = onboardingStatus === "loading" || onboardingStatus === "configuring";

    return (
      <section className="panel onboarding-panel">
        <p className="eyebrow">Setup</p>
        <h1>Start questions</h1>
        <p className="lead">{onboardingMessage}</p>

        <div className="progress-row" role="status" aria-live="polite">
          <span className={`progress-chip ${selectedArea ? "complete" : "active"}`}>1. Skill Area</span>
          <span className={`progress-chip ${selectedMode ? "complete" : onboardingStatus === "awaiting_mode" ? "active" : ""}`}>2. Study Mode</span>
        </div>

        {isBusy && (
          <div className="spinner-inline" aria-label="Loading setup">
            <span className="spinner-dot" />
            <span className="spinner-dot" />
            <span className="spinner-dot" />
          </div>
        )}

        {onboardingStatus === "awaiting_area" && (
          <div className="choices" aria-label="Skill area options">
            {areaOptions.map((option) => (
              <button key={option} className="choice-button" onClick={() => onSelectArea(option)}>
                {option}
              </button>
            ))}
          </div>
        )}

        {onboardingStatus === "awaiting_mode" && (
          <>
            <p className="selection-summary">Selected area: {selectedArea}</p>
            <div className="choices" aria-label="Mode options">
              {modeOptions.map((option) => (
                <button key={option} className="choice-button" onClick={() => onSelectMode(option)}>
                  {option}
                </button>
              ))}
            </div>
          </>
        )}
      </section>
    );
  }

  function renderSessionPage() {
    const showSpinner = sessionStatus === "loading_question" || sessionStatus === "answering";

    return (
      <section className="panel session-panel">
        <header className="session-header">
          <div>
            <p className="eyebrow">Learning Session</p>
            <h1>AI-102 Study Coach</h1>
          </div>
          <button className="ghost-button" onClick={() => navigate("/start")}>Restart setup</button>
        </header>

        <div className="meta-row">
          <p className="meta-chip">Area: {selectedArea}</p>
          <p className="meta-chip">Mode: {selectedMode}</p>
        </div>

        <ul className="message-list">
          {conversation.map((entry) => (
            <li key={entry.id} className={`message-row ${entry.role === "user" ? "user" : "assistant"}`}>
              <article className="message-bubble">
                {entry.kind === "feedback" && (
                  <p className={entry.correct ? "result-correct" : "result-incorrect"}>{entry.correct ? "Correct" : "Incorrect"}</p>
                )}
                <p>{entry.text}</p>
                {entry.memoryRule && <p className="memory-rule">{entry.memoryRule}</p>}
                {entry.verified && <p className="verified">Verified from Learn MCP</p>}
                {entry.warning && <p className="warning">{entry.warning}</p>}

                {(entry.citations?.length ?? 0) > 0 && (
                  <ul className="citation-list">
                    {entry.citations!.map((citation) => (
                      <li key={`${citation.url}-${citation.retrievedAt}`}>
                        <a href={citation.url} target="_blank" rel="noreferrer">
                          {formatCitation(citation)}
                        </a>
                      </li>
                    ))}
                  </ul>
                )}

                {entry.kind === "question" && activeQuestion?.questionId === entry.questionId && route === "/session" && (
                  <div className="choices">
                    {(activeQuestion.choices ?? []).map((choice, index) => {
                      const labeledChoice = withChoiceLabel(choice, index);
                      return (
                        <button key={labeledChoice} className="choice-button" disabled={sessionStatus === "answering"} onClick={() => onAnswer(labeledChoice)}>
                          {labeledChoice}
                        </button>
                      );
                    })}
                  </div>
                )}
              </article>
            </li>
          ))}

          {showSpinner && (
            <li className="message-row assistant" aria-live="polite">
              <article className="message-bubble spinner-bubble" aria-label="Waiting for response">
                <span className="spinner-dot" />
                <span className="spinner-dot" />
                <span className="spinner-dot" />
              </article>
            </li>
          )}
        </ul>
      </section>
    );
  }

  return (
    <main className="layout">
      {route === "/" && renderStartPage()}
      {route === "/start" && renderOnboardingPage()}
      {route === "/session" && renderSessionPage()}
      {error && <p className="error">{error}</p>}
    </main>
  );
}
