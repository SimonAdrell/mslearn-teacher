import { useEffect, useState } from "react";
import { getNextQuestion, getSkillsOutline, startSession, submitAnswer } from "../api";
import type { Citation, QuizAnswerResponse, QuizQuestionResponse, SkillArea } from "../types";
import { createId, MCP_WARNING, MODES, ONBOARDING_STEPS } from "../quizUtils";

export type QuizPhase = "start" | "onboarding" | "session";

export type ConversationEntry = {
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

export type QuizSessionFlow = {
  phase: QuizPhase;
  onboardingStep: number;
  areas: SkillArea[];
  mode: string;
  skillArea: string;
  sessionId: string;
  questionNumber: number;
  conversation: ConversationEntry[];
  activeQuestion: QuizQuestionResponse | null;
  isAnswering: boolean;
  error: string;
  canStart: boolean;
  onboardingProgressValue: number;
  goToOnboarding: () => void;
  backToStart: () => void;
  nextOnboardingStep: () => void;
  previousOnboardingStep: () => void;
  setMode: (mode: string) => void;
  setSkillArea: (skillArea: string) => void;
  onStartSession: () => Promise<void>;
  onAnswer: (choice: string) => Promise<void>;
};

export function useQuizSessionFlow(): QuizSessionFlow {
  const [phase, setPhase] = useState<QuizPhase>("start");
  const [onboardingStep, setOnboardingStep] = useState<number>(1);
  const [areas, setAreas] = useState<SkillArea[]>([]);
  const [mode, setMode] = useState<string>(MODES[0]);
  const [skillArea, setSkillArea] = useState<string>("");
  const [sessionId, setSessionId] = useState<string>("");
  const [questionNumber, setQuestionNumber] = useState<number>(0);
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
  const onboardingProgressValue = (onboardingStep / ONBOARDING_STEPS) * 100;

  async function loadNextQuestion(currentSessionId: string) {
    const question = await getNextQuestion(currentSessionId);
    const hasCitations = (question.citations?.length ?? 0) > 0;
    const hasChoices = (question.choices?.length ?? 0) > 0;

    setQuestionNumber((prev) => prev + 1);
    setConversation((prev) => [
      ...prev,
      {
        id: createId(),
        role: "assistant",
        kind: "question",
        text: question.question,
        citations: question.citations,
        verified: hasCitations,
        warning: hasCitations ? undefined : MCP_WARNING,
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
      setPhase("session");
      setQuestionNumber(0);
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
          warning: hasCitations ? undefined : MCP_WARNING,
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

  function goToOnboarding() {
    setPhase("onboarding");
    setOnboardingStep(1);
    setError("");
  }

  function backToStart() {
    setPhase("start");
    setOnboardingStep(1);
    setError("");
  }

  function nextOnboardingStep() {
    setOnboardingStep((current) => Math.min(ONBOARDING_STEPS, current + 1));
  }

  function previousOnboardingStep() {
    setOnboardingStep((current) => Math.max(1, current - 1));
  }

  return {
    phase,
    onboardingStep,
    areas,
    mode,
    skillArea,
    sessionId,
    questionNumber,
    conversation,
    activeQuestion,
    isAnswering,
    error,
    canStart,
    onboardingProgressValue,
    goToOnboarding,
    backToStart,
    nextOnboardingStep,
    previousOnboardingStep,
    setMode,
    setSkillArea,
    onStartSession,
    onAnswer
  };
}
