import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { App } from "./App";
import * as api from "./api";

vi.mock("./api", async () => {
  const actual = await vi.importActual<typeof import("./api")>("./api");

  return {
    ...actual,
    getSkillsOutline: vi.fn(),
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

    vi.mocked(api.getSkillsOutline).mockResolvedValue({
      areas: [
        {
          name: "Implement natural language processing solutions",
          weightPercent: "30-35%",
          includes: ["Analyze text"]
        }
      ]
    });

    vi.mocked(api.startSession).mockResolvedValue({
      sessionId: "session-1",
      mode: "Quiz",
      skillArea: "Implement natural language processing solutions",
      welcomeMessage: "welcome"
    });

    vi.mocked(api.getNextQuestion).mockResolvedValue({
      questionId: "q1",
      question: "Question",
      choices: ["Option one", "Option two", "Option three"],
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
      citations: [
        {
          title: "What is Azure AI Language?",
          url: "https://learn.microsoft.com/en-us/azure/ai-services/language-service/overview",
          retrievedAt: "2026-03-06"
        }
      ]
    });
  });

  it("auto-starts in Quiz mode and loads the first question", async () => {
    render(<App />);

    await waitFor(() =>
      expect(api.startSession).toHaveBeenCalledWith("Quiz", "Implement natural language processing solutions")
    );
    await waitFor(() => expect(api.getNextQuestion).toHaveBeenCalledWith("session-1"));

    expect(await screen.findByRole("button", { name: "A) Option one" })).toBeInTheDocument();
  });

  it("renders chat metadata, verification badge, and citation dates", async () => {
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
      meta: {
        skillOutlineArea: "Implement natural language processing solutions",
        mustKnow: ["Use Azure AI Language for intent and entities"],
        examTraps: ["Confusing Azure OpenAI with Azure AI Language"],
        mcpVerified: true,
        weakAreasUpdate: ["Implement natural language processing solutions"]
      }
    });

    render(<App />);

    await waitFor(() => expect(api.startSession).toHaveBeenCalledTimes(1));

    fireEvent.change(screen.getByPlaceholderText("Ask an AI-102 question..."), {
      target: { value: "Teach me about intent classification" }
    });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await screen.findByText("Verified from Learn MCP");
    expect(screen.getByText("Prioritized next topic: Implement natural language processing solutions")).toBeInTheDocument();

    fireEvent.click(screen.getByText("Exam Focus"));
    expect(screen.getByText(/Skill area:/)).toBeInTheDocument();
    expect(screen.getAllByRole("link", { name: /2026-03-06/ }).length).toBeGreaterThan(0);
  });

  it("renders chat and quiz panels in the interaction grid", async () => {
    const { container } = render(<App />);

    await waitFor(() => expect(api.startSession).toHaveBeenCalledTimes(1));

    expect(container.querySelector(".interaction-grid")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Chat" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Quiz" })).toBeInTheDocument();
  });

  it("labels quiz options as A/B/C", async () => {
    render(<App />);

    await screen.findByRole("button", { name: "A) Option one" });
    expect(screen.getByRole("button", { name: "B) Option two" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "C) Option three" })).toBeInTheDocument();
  });
});
