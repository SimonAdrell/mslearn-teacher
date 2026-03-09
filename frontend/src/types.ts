export type TokenUsage = {
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
};

export type BootstrapSessionResponse = {
  sessionId: string;
  message: string;
  areaOptions: string[];
  modeOptions: string[];
  usage?: TokenUsage;
};

export type SkillArea = {
  name: string;
  weightPercent: string;
  includes: string[];
};

export type SkillsOutlineResponse = {
  areas: SkillArea[];
  citations?: Citation[];
  isFromCache?: boolean;
};

export type Citation = {
  title: string;
  url: string;
  retrievedAt: string;
};

export type StartSessionResponse = {
  sessionId: string;
  mode: string;
  skillArea: string;
  welcomeMessage: string;
};

export type ChatMeta = {
  skillOutlineArea: string;
  mustKnow: string[];
  examTraps: string[];
  mcpVerified: boolean;
  weakAreasUpdate?: string[];
};

export type ChatResponse = {
  answer: string;
  citations: Citation[];
  refused: boolean;
  refusalReason?: string;
  meta?: ChatMeta;
  usage?: TokenUsage;
};

export type QuizQuestionResponse = {
  questionId: string;
  question: string;
  choices?: string[];
  citations?: Citation[];
  usage?: TokenUsage;
};

export type QuizAnswerResponse = {
  correct: boolean;
  explanation: string;
  memoryRule: string;
  citations: Citation[];
  usage?: TokenUsage;
};
