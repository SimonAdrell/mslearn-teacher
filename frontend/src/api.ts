import type {
  BootstrapSessionResponse,
  ChatResponse,
  QuizAnswerResponse,
  QuizQuestionResponse,
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

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

export function bootstrapSession() {
  return request<BootstrapSessionResponse>("/api/study/session/bootstrap", {
    method: "POST"
  });
}

export function configureSession(sessionId: string, mode: string, skillArea: string) {
  return request<void>("/api/study/session/configure", {
    method: "POST",
    body: JSON.stringify({ sessionId, mode, skillArea })
  });
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
