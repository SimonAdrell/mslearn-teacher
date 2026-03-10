import type { Citation } from "./types";

export const MODES = ["Learn", "Quiz", "Review mistakes", "Rapid cram"] as const;
export const ONBOARDING_STEPS = 2;
export const MCP_WARNING = "I can't answer this from verified Microsoft Learn MCP sources right now.";

export function formatCitation(citation: Citation) {
  return `${citation.title} (${citation.retrievedAt})`;
}

export function withChoiceLabel(choice: string, index: number) {
  const labels = ["A", "B", "C"];
  const label = labels[index] ?? String.fromCharCode(65 + index);

  if (/^[A-C]\)/i.test(choice.trim())) {
    return choice;
  }

  return `${label}) ${choice}`;
}

export function createId() {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}
