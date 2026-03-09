import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { App } from "./App";
import * as api from "./api";

vi.mock("./api", async () => {
  const actual = await vi.importActual<typeof import("./api")>("./api");

  return {
    ...actual,
    bootstrapSession: vi.fn(),
    startSession: vi.fn(),
    sendChat: vi.fn(),
    getNextQuestion: vi.fn(),
    submitAnswer: vi.fn()
  };
});

describe("App", () => {
  afterEach(() => {
    cleanup();
  });

  beforeEach(() => {
    vi.resetAllMocks();

    vi.mocked(api.bootstrapSession).mockResolvedValue({
      sessionId: "bootstrap-session-1",
      message: "Let's start your AI-102 session. Pick a skill area.",
      areaOptions: ["Implement natural language processing solutions (30-35%)"],
      modeOptions: ["Learn", "Quiz", "Review mistakes", "Rapid cram"],
      usage: {
        promptTokens: 10,
        completionTokens: 5,
        totalTokens: 15
      }
    });

    vi.mocked(api.startSession).mockResolvedValue({
      sessionId: "session-1",
      mode: "Learn",
      skillArea: "Implement natural language processing solutions (30-35%)",
      welcomeMessage: "welcome"
    });

    vi.mocked(api.sendChat).mockResolvedValue({
      answer: "Use Azure AI Language for intent and entities.",
      refused: false,
      citations: [
        {
          title: "What is Azure AI Language?",
          url: "https://learn.microsoft.com/en-us/azure/ai-services/language-service/overview",
          retrievedAt: "2026-03-06"
        }
      ],
      usage: {
        promptTokens: 20,
        completionTokens: 10,
        totalTokens: 30
      },
      meta: {
        skillOutlineArea: "Implement natural language processing solutions",
        mustKnow: ["Use Azure AI Language for intent and entities"],
        examTraps: ["Confusing Azure OpenAI with Azure AI Language"],
        mcpVerified: true,
        weakAreasUpdate: ["Implement natural language processing solutions"]
      }
    });

    vi.mocked(api.getNextQuestion).mockResolvedValue({
      questionId: "q1",
      question: "Question",
      choices: ["Option one", "Option two", "Option three"],
      usage: {
        promptTokens: 8,
        completionTokens: 4,
        totalTokens: 12
      },
      citations: [
        {
          title: "What is Azure AI Language?",
          url: "https://learn.microsoft.com/en-us/azure/ai-services/language-service/overview",
          retrievedAt: "2026-03-06"
        }
      ]
    });

    vi.mocked(api.submitAnswer).mockResolvedValue({
      correct: true,
      explanation: "Because",
      memoryRule: "Rule",
      usage: {
        promptTokens: 6,
        completionTokens: 3,
        totalTokens: 9
      },
      citations: [
        {
          title: "What is Azure AI Language?",
          url: "https://learn.microsoft.com/en-us/azure/ai-services/language-service/overview",
          retrievedAt: "2026-03-06"
        }
      ]
    });
  });

  it("shows bootstrap usage, per-response usage, and running token totals", async () => {
    render(<App />);

    await screen.findByText("Let's start your AI-102 session. Pick a skill area.");
    expect(api.bootstrapSession).toHaveBeenCalledTimes(1);
    expect(screen.getByText("PromptTokens: 10 | CompletionTokens: 5 | TotalTokens: 15")).toBeInTheDocument();

    const startButton = screen.getByRole("button", { name: "Start Session" });
    await waitFor(() => expect(startButton).not.toBeDisabled());
    fireEvent.click(startButton);
    await waitFor(() => expect(api.startSession).toHaveBeenCalledTimes(1));

    fireEvent.change(screen.getByPlaceholderText("Ask an AI-102 question..."), {
      target: { value: "Teach me about intent classification" }
    });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await screen.findByText("Use Azure AI Language for intent and entities.");
    expect(screen.getByText("PromptTokens: 20 | CompletionTokens: 10 | TotalTokens: 30")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Next Question" }));
    await screen.findByText("Question");
    expect(screen.getByText("PromptTokens: 8 | CompletionTokens: 4 | TotalTokens: 12")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "A) Option one" }));
    await screen.findByText("Because");
    expect(screen.getByText("PromptTokens: 6 | CompletionTokens: 3 | TotalTokens: 9")).toBeInTheDocument();

    expect(screen.getByText("PromptTokens: 44")).toBeInTheDocument();
    expect(screen.getByText("CompletionTokens: 22")).toBeInTheDocument();
    expect(screen.getByText("TotalTokens: 66")).toBeInTheDocument();
  });

  it("labels quiz options as A/B/C", async () => {
    render(<App />);

    const startButton = screen.getByRole("button", { name: "Start Session" });
    await waitFor(() => expect(startButton).not.toBeDisabled());
    fireEvent.click(startButton);
    await waitFor(() => expect(api.startSession).toHaveBeenCalledTimes(1));

    fireEvent.click(screen.getByRole("button", { name: "Next Question" }));

    await screen.findByRole("button", { name: "A) Option one" });
    expect(screen.getByRole("button", { name: "B) Option two" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "C) Option three" })).toBeInTheDocument();
  });
});
