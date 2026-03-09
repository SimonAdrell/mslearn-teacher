import { useEffect, useState } from "react";
import { getNextQuestion, getSkillsOutline, startSession, submitAnswer } from "./api";
import type { Citation, QuizAnswerResponse, QuizQuestionResponse, SkillArea } from "./types";

const MODES = ["Learn", "Quiz", "Review mistakes", "Rapid cram"] as const;

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

export function App() {
  const [areas, setAreas] = useState<SkillArea[]>([]);
  const [mode, setMode] = useState<string>(MODES[0]);
  const [skillArea, setSkillArea] = useState<string>("");
  const [sessionId, setSessionId] = useState<string>("");
  const [conversation, setConversation] = useState<ConversationEntry[]>([]);
  const [activeQuestion, setActiveQuestion] = useState<QuizQuestionResponse | null>(null);
  const [isAnswering, setIsAnswering] = useState<boolean>(false);
  const [error, setError] = useState<string>("");

  useEffect(() => {
    getSkillsOutline()
      .then((result) => {
        setAreas(result.areas);
        setSkillArea(result.areas[0]?.name ?? "");
      })
      .catch((err: Error) => setError(err.message));
  }, []);

  const canStart = mode.length > 0 && skillArea.length > 0;

  async function loadNextQuestion(currentSessionId: string) {
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
  }

  async function onStartSession() {
    try {
      setError("");
      const response = await startSession(mode, skillArea);
      setSessionId(response.sessionId);
      setConversation([
        {
          id: createId(),
          role: "assistant",
          kind: "info",
          text: response.welcomeMessage
        }
      ]);
      setActiveQuestion(null);
      await loadNextQuestion(response.sessionId);
    } catch (err) {
      setError((err as Error).message);
    }
  }

  async function onAnswer(choice: string) {
    if (!sessionId || !activeQuestion || isAnswering) return;

    try {
      setIsAnswering(true);
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
    } finally {
      setIsAnswering(false);
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

      <section className="panel chat-panel">
        <h2>Quiz Chat</h2>
        {!sessionId && <p className="hint">Start a session to begin the chat-style quiz.</p>}

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

                {entry.kind === "question" && activeQuestion?.questionId === entry.questionId && (
                  <div className="choices">
                    {(activeQuestion.choices ?? []).map((choice, index) => {
                      const labeledChoice = withChoiceLabel(choice, index);
                      return (
                        <button key={labeledChoice} disabled={isAnswering} onClick={() => onAnswer(labeledChoice)}>
                          {labeledChoice}
                        </button>
                      );
                    })}
                  </div>
                )}
              </article>
            </li>
          ))}
        </ul>

        {isAnswering && <p>Thinking...</p>}
      </section>

      {error && <p className="error">{error}</p>}
    </main>
  );
}
