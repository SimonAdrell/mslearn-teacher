import { useEffect, useState } from "react";
import { getNextQuestion, getSkillsOutline, startSession, submitAnswer } from "./api";
import type { Citation, QuizAnswerResponse, QuizQuestionResponse, SkillArea } from "./types";

const MODES = ["Learn", "Quiz", "Review mistakes", "Rapid cram"] as const;

type OnboardingPhase = "loading_areas" | "awaiting_area" | "awaiting_mode" | "starting_session" | "session_active";

type ConversationEntry = {
  id: string;
  role: "assistant" | "user";
  kind: "info" | "question" | "feedback" | "selection" | "area_prompt" | "mode_prompt";
  text: string;
  citations?: Citation[];
  verified?: boolean;
  warning?: string;
  correct?: boolean;
  memoryRule?: string;
  questionId?: string;
  areaOptions?: string[];
  modeOptions?: string[];
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

export function App() {
  const [sessionId, setSessionId] = useState<string>("");
  const [areas, setAreas] = useState<SkillArea[]>([]);
  const [selectedArea, setSelectedArea] = useState<string>("");
  const [selectedMode, setSelectedMode] = useState<string>("");
  const [onboardingPhase, setOnboardingPhase] = useState<OnboardingPhase>("loading_areas");
  const [conversation, setConversation] = useState<ConversationEntry[]>([]);
  const [activeQuestion, setActiveQuestion] = useState<QuizQuestionResponse | null>(null);
  const [isAnswering, setIsAnswering] = useState<boolean>(false);
  const [error, setError] = useState<string>("");

  useEffect(() => {
    async function initializeOnboarding() {
      try {
        setError("");
        const result = await getSkillsOutline();
        setAreas(result.areas);

        if (result.areas.length === 0) {
          setConversation([
            {
              id: createId(),
              role: "assistant",
              kind: "info",
              text: "I could not find any skill outline areas right now. Please try again shortly."
            }
          ]);
          return;
        }

        setConversation([
          {
            id: createId(),
            role: "assistant",
            kind: "area_prompt",
            text: "Let's start your AI-102 session. Pick a Skill Outline Area.",
            areaOptions: result.areas.map((area) => `${area.name} (${area.weightPercent})`)
          }
        ]);
        setOnboardingPhase("awaiting_area");
      } catch (err) {
        setError((err as Error).message);
      }
    }

    void initializeOnboarding();
  }, []);

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

  function getAreaName(option: string) {
    const match = areas.find((area) => option.startsWith(`${area.name} (`));
    return match?.name ?? option;
  }

  async function onSelectArea(option: string) {
    if (onboardingPhase !== "awaiting_area") return;

    const areaName = getAreaName(option);
    setSelectedArea(areaName);
    setOnboardingPhase("awaiting_mode");

    setConversation((prev) => [
      ...prev,
      {
        id: createId(),
        role: "user",
        kind: "selection",
        text: areaName
      },
      {
        id: createId(),
        role: "assistant",
        kind: "mode_prompt",
        text: "Great. Choose your study mode.",
        modeOptions: [...MODES]
      }
    ]);
  }

  async function onSelectMode(mode: string) {
    if (onboardingPhase !== "awaiting_mode" || !selectedArea) return;

    setSelectedMode(mode);
    setOnboardingPhase("starting_session");

    setConversation((prev) => [
      ...prev,
      {
        id: createId(),
        role: "user",
        kind: "selection",
        text: mode
      }
    ]);

    try {
      setError("");
      const session = await startSession(mode, selectedArea);
      setSessionId(session.sessionId);

      setConversation((prev) => [
        ...prev,
        {
          id: createId(),
          role: "assistant",
          kind: "info",
          text: session.welcomeMessage
        }
      ]);

      setOnboardingPhase("session_active");
      await loadNextQuestion(session.sessionId);
    } catch (err) {
      setError((err as Error).message);
      setOnboardingPhase("awaiting_mode");
    }
  }

  async function onAnswer(choice: string) {
    if (!sessionId || !activeQuestion || onboardingPhase !== "session_active" || isAnswering) return;

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

      <section className="panel chat-panel">
        <h2>Quiz Chat</h2>
        {selectedArea && <p className="hint">Area: {selectedArea}</p>}
        {selectedMode && <p className="hint">Mode: {selectedMode}</p>}

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

                {entry.kind === "area_prompt" && onboardingPhase === "awaiting_area" && (
                  <div className="choices">
                    {(entry.areaOptions ?? []).map((option) => (
                      <button key={option} onClick={() => onSelectArea(option)}>
                        {option}
                      </button>
                    ))}
                  </div>
                )}

                {entry.kind === "mode_prompt" && onboardingPhase === "awaiting_mode" && (
                  <div className="choices">
                    {(entry.modeOptions ?? []).map((option) => (
                      <button key={option} onClick={() => onSelectMode(option)}>
                        {option}
                      </button>
                    ))}
                  </div>
                )}

                {entry.kind === "question" && activeQuestion?.questionId === entry.questionId && onboardingPhase === "session_active" && (
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

        {(onboardingPhase === "loading_areas" || onboardingPhase === "starting_session" || isAnswering) && <p>Thinking...</p>}
      </section>

      {error && <p className="error">{error}</p>}
    </main>
  );
}

