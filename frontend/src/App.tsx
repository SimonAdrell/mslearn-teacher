import { useEffect, useMemo, useRef, useState } from "react";
import { getNextQuestion, getSkillsOutline, sendChat, startSession, submitAnswer } from "./api";
import type { ChatResponse, Citation, QuizAnswerResponse, QuizQuestionResponse, SkillArea } from "./types";

const MODES = ["Learn", "Quiz", "Review mistakes", "Rapid cram"] as const;
const QUIZ_MODE = "Quiz";

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

export function App() {
  const [areas, setAreas] = useState<SkillArea[]>([]);
  const [mode, setMode] = useState<string>(QUIZ_MODE);
  const [skillArea, setSkillArea] = useState<string>("");
  const [sessionId, setSessionId] = useState<string>("");
  const [chatInput, setChatInput] = useState<string>("");
  const [chatLog, setChatLog] = useState<ChatResponse[]>([]);
  const [quizQuestion, setQuizQuestion] = useState<QuizQuestionResponse | null>(null);
  const [quizFeedback, setQuizFeedback] = useState<QuizAnswerResponse | null>(null);
  const [weakAreaCounts, setWeakAreaCounts] = useState<Record<string, number>>({});
  const [error, setError] = useState<string>("");
  const hasAutoStartedSession = useRef(false);

  const canStart = useMemo(() => mode.length > 0 && skillArea.length > 0, [mode, skillArea]);

  const prioritizedWeakArea = useMemo(() => {
    const sorted = Object.entries(weakAreaCounts).sort((a, b) => b[1] - a[1]);
    return sorted[0]?.[0];
  }, [weakAreaCounts]);

  async function initializeSession(
    sessionMode: string,
    selectedSkillArea: string,
    loadFirstQuestion: boolean,
    shouldAbort: () => boolean = () => false
  ) {
    setError("");
    const response = await startSession(sessionMode, selectedSkillArea);

    if (shouldAbort()) {
      return;
    }

    setSessionId(response.sessionId);
    setChatLog([]);
    setQuizQuestion(null);
    setQuizFeedback(null);
    setWeakAreaCounts({});

    if (loadFirstQuestion) {
      const question = await getNextQuestion(response.sessionId);
      if (shouldAbort()) {
        return;
      }

      setQuizQuestion(question);
    }
  }

  useEffect(() => {
    let cancelled = false;

    async function bootstrap() {
      try {
        const result = await getSkillsOutline();
        if (cancelled) {
          return;
        }

        setAreas(result.areas);

        const defaultArea = result.areas[0]?.name ?? "";
        setSkillArea(defaultArea);

        if (defaultArea.length === 0 || hasAutoStartedSession.current) {
          return;
        }

        hasAutoStartedSession.current = true;
        setMode(QUIZ_MODE);
        await initializeSession(QUIZ_MODE, defaultArea, true, () => cancelled);
      } catch (err) {
        if (!cancelled) {
          setError((err as Error).message);
        }
      }
    }

    bootstrap();

    return () => {
      cancelled = true;
    };
  }, []);

  async function onStartSession() {
    try {
      await initializeSession(mode, skillArea, mode === QUIZ_MODE);
    } catch (err) {
      setError((err as Error).message);
    }
  }

  async function onSendChat() {
    if (!sessionId || !chatInput.trim()) return;

    try {
      setError("");
      const response = await sendChat(sessionId, chatInput);
      setChatLog((prev) => [...prev, response]);
      setChatInput("");

      const weakAreas = response.meta?.weakAreasUpdate ?? [];
      if (weakAreas.length > 0) {
        setWeakAreaCounts((prev) => {
          const next = { ...prev };
          weakAreas.forEach((area) => {
            next[area] = (next[area] ?? 0) + 1;
          });
          return next;
        });
      }
    } catch (err) {
      setError((err as Error).message);
    }
  }

  async function onNextQuiz() {
    if (!sessionId) return;

    try {
      setError("");
      setQuizFeedback(null);
      const response = await getNextQuestion(sessionId);
      setQuizQuestion(response);
    } catch (err) {
      setError((err as Error).message);
    }
  }

  async function onAnswer(choice: string) {
    if (!sessionId || !quizQuestion) return;
    try {
      setError("");
      const response = await submitAnswer(sessionId, quizQuestion.questionId, choice);
      setQuizFeedback(response);
    } catch (err) {
      setError((err as Error).message);
    }
  }

  return (
    <main className="layout">
      <h1>AI-102 Study Coach</h1>

      <section className="panel">
        <h2>Session Setup</h2>
        <div className="row">
          <label>Mode</label>
          <select value={mode} onChange={(e) => setMode(e.target.value)}>
            {MODES.map((item) => (
              <option key={item} value={item}>
                {item}
              </option>
            ))}
          </select>
        </div>
        <div className="row">
          <label>Skill Outline Area</label>
          <select value={skillArea} onChange={(e) => setSkillArea(e.target.value)}>
            {areas.map((area) => (
              <option key={area.name} value={area.name}>
                {area.name} ({area.weightPercent})
              </option>
            ))}
          </select>
        </div>
        <button disabled={!canStart} onClick={onStartSession}>
          Start Session
        </button>
        {sessionId && <p>Session: {sessionId}</p>}
      </section>

      <div className="interaction-grid">
        <section className="panel">
          <h2>Chat</h2>
          <div className="row">
            <input value={chatInput} onChange={(e) => setChatInput(e.target.value)} placeholder="Ask an AI-102 question..." />
            <button disabled={!sessionId} onClick={onSendChat}>
              Send
            </button>
          </div>

          {prioritizedWeakArea && <p className="hint">Prioritized next topic: {prioritizedWeakArea}</p>}

          <ul className="chat-list">
            {chatLog.map((entry, index) => (
              <li key={`${entry.answer}-${index}`} className="chat-entry">
                <p>{entry.answer}</p>
                {entry.meta?.mcpVerified && <p className="verified">Verified from Learn MCP</p>}
                {entry.refused && <p className="warning">{entry.refusalReason}</p>}

                {entry.meta && (
                  <details>
                    <summary>Exam Focus</summary>
                    <p>Skill area: {entry.meta.skillOutlineArea}</p>
                    {entry.meta.mustKnow.length > 0 && <p>Must-know: {entry.meta.mustKnow.join("; ")}</p>}
                    {entry.meta.examTraps.length > 0 && <p>Exam traps: {entry.meta.examTraps.join("; ")}</p>}
                  </details>
                )}

                {entry.citations.length > 0 && (
                  <ul className="citation-list">
                    {entry.citations.map((citation) => (
                      <li key={`${citation.url}-${citation.retrievedAt}`}>
                        <a href={citation.url} target="_blank" rel="noreferrer">
                          {formatCitation(citation)}
                        </a>
                      </li>
                    ))}
                  </ul>
                )}
              </li>
            ))}
          </ul>
        </section>

        <section className="panel">
          <h2>Quiz</h2>
          <button disabled={!sessionId} onClick={onNextQuiz}>
            Next Question
          </button>
          {quizQuestion && (
            <div>
              <p>{quizQuestion.question}</p>
              <div className="choices">
                {(quizQuestion.choices ?? []).map((choice, index) => {
                  const labeledChoice = withChoiceLabel(choice, index);
                  return (
                    <button key={labeledChoice} onClick={() => onAnswer(labeledChoice)}>
                      {labeledChoice}
                    </button>
                  );
                })}
              </div>

              {(quizQuestion.citations?.length ?? 0) > 0 && (
                <ul className="citation-list">
                  {quizQuestion.citations!.map((citation) => (
                    <li key={`${citation.url}-${citation.retrievedAt}`}>
                      <a href={citation.url} target="_blank" rel="noreferrer">
                        {formatCitation(citation)}
                      </a>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
          {quizFeedback && (
            <div>
              <p>{quizFeedback.correct ? "Correct" : "Incorrect"}</p>
              <p>{quizFeedback.explanation}</p>
              <p>{quizFeedback.memoryRule}</p>
              <ul className="citation-list">
                {quizFeedback.citations.map((citation) => (
                  <li key={`${citation.url}-${citation.retrievedAt}`}>
                    <a href={citation.url} target="_blank" rel="noreferrer">
                      {formatCitation(citation)}
                    </a>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </section>
      </div>

      {error && <p className="error">{error}</p>}
    </main>
  );
}

