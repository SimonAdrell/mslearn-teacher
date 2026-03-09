import { useEffect, useMemo, useState } from "react";
import { bootstrapSession, getNextQuestion, sendChat, startSession, submitAnswer } from "./api";
import type { ChatResponse, Citation, QuizAnswerResponse, QuizQuestionResponse, TokenUsage } from "./types";

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

function formatUsage(usage: TokenUsage) {
  return `PromptTokens: ${usage.promptTokens} | CompletionTokens: ${usage.completionTokens} | TotalTokens: ${usage.totalTokens}`;
}

export function App() {
  const [areaOptions, setAreaOptions] = useState<string[]>([]);
  const [modeOptions, setModeOptions] = useState<string[]>([...MODES]);
  const [mode, setMode] = useState<string>(MODES[0]);
  const [skillArea, setSkillArea] = useState<string>("");
  const [sessionId, setSessionId] = useState<string>("");
  const [chatInput, setChatInput] = useState<string>("");
  const [chatLog, setChatLog] = useState<ChatResponse[]>([]);
  const [quizQuestion, setQuizQuestion] = useState<QuizQuestionResponse | null>(null);
  const [quizFeedback, setQuizFeedback] = useState<QuizAnswerResponse | null>(null);
  const [weakAreaCounts, setWeakAreaCounts] = useState<Record<string, number>>({});
  const [bootstrapMessage, setBootstrapMessage] = useState<string>("Preparing session setup options...");
  const [bootstrapUsage, setBootstrapUsage] = useState<TokenUsage | undefined>(undefined);
  const [usageLog, setUsageLog] = useState<TokenUsage[]>([]);
  const [error, setError] = useState<string>("");

  useEffect(() => {
    bootstrapSession()
      .then((result) => {
        setBootstrapMessage(result.message);
        setAreaOptions(result.areaOptions);
        setModeOptions(result.modeOptions);
        setMode((currentMode) => (result.modeOptions.includes(currentMode) ? currentMode : (result.modeOptions[0] ?? MODES[0])));
        setSkillArea(result.areaOptions[0] ?? "");
        setBootstrapUsage(result.usage);
        setUsageLog(result.usage ? [result.usage] : []);
      })
      .catch((err: Error) => setError(err.message));
  }, []);

  const canStart = useMemo(() => mode.length > 0 && skillArea.length > 0, [mode, skillArea]);

  const prioritizedWeakArea = useMemo(() => {
    const sorted = Object.entries(weakAreaCounts).sort((a, b) => b[1] - a[1]);
    return sorted[0]?.[0];
  }, [weakAreaCounts]);

  const tokenTotals = useMemo(() => {
    return usageLog.reduce(
      (total, usage) => {
        total.promptTokens += usage.promptTokens;
        total.completionTokens += usage.completionTokens;
        total.totalTokens += usage.totalTokens;
        return total;
      },
      { promptTokens: 0, completionTokens: 0, totalTokens: 0 }
    );
  }, [usageLog]);

  async function onStartSession() {
    try {
      setError("");
      const response = await startSession(mode, skillArea);
      setSessionId(response.sessionId);
      setChatLog([]);
      setQuizQuestion(null);
      setQuizFeedback(null);
      setWeakAreaCounts({});
      setUsageLog(bootstrapUsage ? [bootstrapUsage] : []);
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
      if (response.usage) {
        setUsageLog((prev) => [...prev, response.usage!]);
      }
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
      if (response.usage) {
        setUsageLog((prev) => [...prev, response.usage!]);
      }
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
      if (response.usage) {
        setUsageLog((prev) => [...prev, response.usage!]);
      }
    } catch (err) {
      setError((err as Error).message);
    }
  }

  return (
    <main className="layout">
      <h1>AI-102 Study Coach</h1>
      <section className="panel">
        <h2>Session Setup</h2>
        <p className="hint">{bootstrapMessage}</p>
        {bootstrapUsage && <p className="token-usage">{formatUsage(bootstrapUsage)}</p>}

        <div className="row">
          <label>Mode</label>
          <select value={mode} onChange={(e) => setMode(e.target.value)}>
            {modeOptions.map((item) => (
              <option key={item} value={item}>
                {item}
              </option>
            ))}
          </select>
        </div>
        <div className="row">
          <label>Skill Outline Area</label>
          <select value={skillArea} onChange={(e) => setSkillArea(e.target.value)}>
            {areaOptions.map((area) => (
              <option key={area} value={area}>
                {area}
              </option>
            ))}
          </select>
        </div>
        <button disabled={!canStart} onClick={onStartSession}>
          Start Session
        </button>
        {sessionId && <p>Session: {sessionId}</p>}
        {sessionId && (
          <div className="token-total-row" role="status" aria-live="polite">
            <p className="meta-chip">PromptTokens: {tokenTotals.promptTokens}</p>
            <p className="meta-chip">CompletionTokens: {tokenTotals.completionTokens}</p>
            <p className="meta-chip">TotalTokens: {tokenTotals.totalTokens}</p>
          </div>
        )}
      </section>

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
              {entry.usage && <p className="token-usage">{formatUsage(entry.usage)}</p>}
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
            {quizQuestion.usage && <p className="token-usage">{formatUsage(quizQuestion.usage)}</p>}
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
            {quizFeedback.usage && <p className="token-usage">{formatUsage(quizFeedback.usage)}</p>}
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

      {error && <p className="error">{error}</p>}
    </main>
  );
}
