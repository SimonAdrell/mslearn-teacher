import type {
  BootstrapSessionResponse,
  ChatResponse,
  QuizAnswerResponse,
  QuizQuestionResponse,
  SkillsOutlineResponse,
  StartSessionResponse
} from "./types";

type AccessTokenProvider = () => Promise<string | null>;

let accessTokenProvider: AccessTokenProvider = async () => null;

export function configureApiAuth(provider: AccessTokenProvider) {
  accessTokenProvider = provider;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const accessToken = await accessTokenProvider();

  const response = await fetch(path, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      ...(init?.headers ?? {})
    }
  });

  if (!response.ok) {
    throw new Error(`Request failed (${response.status})`);
  }

  return response.json() as Promise<T>;
}

export function bootstrapSession() {
  return request<BootstrapSessionResponse>("/api/study/session/bootstrap", {
    method: "POST"
  });
}

export function getSkillsOutline() {
  return request<SkillsOutlineResponse>("/api/study/skills-outline");
}

export function startSession(mode: string, skillArea: string) {
  return request<StartSessionResponse>("/api/study/session/start", {
    method: "POST",
    body: JSON.stringify({ mode, skillArea })
  });
}

export function sendChat(sessionId: string, message: string) {
  return request<ChatResponse>("/api/study/chat", {
    method: "POST",
    body: JSON.stringify({ sessionId, message })
  });
}

export function getNextQuestion(sessionId: string) {
  return request<QuizQuestionResponse>("/api/study/quiz/next", {
    method: "POST",
    body: JSON.stringify({ sessionId })
  });
}

export function submitAnswer(sessionId: string, questionId: string, answer: string) {
  return request<QuizAnswerResponse>("/api/study/quiz/answer", {
    method: "POST",
    body: JSON.stringify({ sessionId, questionId, answer })
  });
}
