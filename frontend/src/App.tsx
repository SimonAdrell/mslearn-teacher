import { useEffect, useMemo, useState } from "react";
import { getNextQuestion, getSkillsOutline, sendChat, startSession, submitAnswer } from "./api";
import type { ChatResponse, QuizAnswerResponse, QuizQuestionResponse, SkillArea } from "./types";

const MODES = ["Learn", "Quiz", "Review mistakes", "Rapid cram"] as const;

export function App() {
  const [areas, setAreas] = useState<SkillArea[]>([]);
  const [mode, setMode] = useState<string>(MODES[0]);
  const [skillArea, setSkillArea] = useState<string>("");
  const [sessionId, setSessionId] = useState<string>("");
  const [chatInput, setChatInput] = useState<string>("");
  const [chatLog, setChatLog] = useState<ChatResponse[]>([]);
  const [quizQuestion, setQuizQuestion] = useState<QuizQuestionResponse | null>(null);
  const [quizFeedback, setQuizFeedback] = useState<QuizAnswerResponse | null>(null);
  const [error, setError] = useState<string>("");

  useEffect(() => {
    getSkillsOutline()
      .then((result) => {
        setAreas(result.areas);
        setSkillArea(result.areas[0]?.name ?? "");
      })
      .catch((err: Error) => setError(err.message));
  }, []);

  const canStart = useMemo(() => mode.length > 0 && skillArea.length > 0, [mode, skillArea]);

  async function onStartSession() {
    try {
      setError("");
      const response = await startSession(mode, skillArea);
      setSessionId(response.sessionId);
      setChatLog([]);
      setQuizQuestion(null);
      setQuizFeedback(null);
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

      <section className="panel">
        <h2>Chat</h2>
        <div className="row">
          <input value={chatInput} onChange={(e) => setChatInput(e.target.value)} placeholder="Ask an AI-102 question..." />
          <button disabled={!sessionId} onClick={onSendChat}>
            Send
          </button>
        </div>
        <ul>
          {chatLog.map((entry, index) => (
            <li key={`${entry.answer}-${index}`}>
              <p>{entry.answer}</p>
              {entry.refused && <p className="warning">{entry.refusalReason}</p>}
              {entry.citations.map((citation) => (
                <a key={citation.url} href={citation.url} target="_blank" rel="noreferrer">
                  {citation.title}
                </a>
              ))}
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
              {(quizQuestion.choices ?? []).map((choice) => (
                <button key={choice} onClick={() => onAnswer(choice)}>
                  {choice}
                </button>
              ))}
            </div>
          </div>
        )}
        {quizFeedback && (
          <div>
            <p>{quizFeedback.correct ? "Correct" : "Incorrect"}</p>
            <p>{quizFeedback.explanation}</p>
            <p>{quizFeedback.memoryRule}</p>
            {quizFeedback.citations.map((citation) => (
              <a key={citation.url} href={citation.url} target="_blank" rel="noreferrer">
                {citation.title}
              </a>
            ))}
          </div>
        )}
      </section>

      {error && <p className="error">{error}</p>}
    </main>
  );
}
