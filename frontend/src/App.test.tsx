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
      mode: "Learn",
      skillArea: "Implement natural language processing solutions",
      welcomeMessage: "welcome"
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

  it("uses option-only quiz chat and starts flow after session start", async () => {
    vi.mocked(api.getNextQuestion).mockResolvedValue({
      questionId: "q1",
      question: "Question 1",
      choices: ["Option one", "Option two", "Option three"],
      citations: [
        {
          title: "What is Azure AI Language?",
          url: "https://learn.microsoft.com/en-us/azure/ai-services/language-service/overview",
          retrievedAt: "2026-03-06"
        }
      ]
    });

    render(<App />);

    expect(screen.queryByPlaceholderText("Ask an AI-102 question...")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "A) Option one" })).not.toBeInTheDocument();

    const startButton = screen.getByRole("button", { name: "Start Session" });
    await waitFor(() => expect(startButton).not.toBeDisabled());
    fireEvent.click(startButton);

    await screen.findByRole("button", { name: "A) Option one" });
    expect(api.getNextQuestion).toHaveBeenCalledTimes(1);
  });

  it("appends user choice, feedback, and auto-loads next question", async () => {
    vi.mocked(api.getNextQuestion)
      .mockResolvedValueOnce({
        questionId: "q1",
        question: "Question 1",
        choices: ["Option one", "Option two", "Option three"],
        citations: [
          {
            title: "What is Azure AI Language?",
            url: "https://learn.microsoft.com/en-us/azure/ai-services/language-service/overview",
            retrievedAt: "2026-03-06"
          }
        ]
      })
      .mockResolvedValueOnce({
        questionId: "q2",
        question: "Question 2",
        choices: ["Next one", "Next two", "Next three"],
        citations: [
          {
            title: "Language service overview",
            url: "https://learn.microsoft.com/en-us/azure/ai-services/language-service/overview",
            retrievedAt: "2026-03-06"
          }
        ]
      });

    render(<App />);

    const startButton = screen.getByRole("button", { name: "Start Session" });
    await waitFor(() => expect(startButton).not.toBeDisabled());
    fireEvent.click(startButton);

    const optionButton = await screen.findByRole("button", { name: "A) Option one" });
    fireEvent.click(optionButton);

    await screen.findByText("A) Option one");
    await screen.findByText("Correct");
    await screen.findByText("Because");
    await screen.findByText("Rule");
    await screen.findByText("Question 2");

    expect(api.submitAnswer).toHaveBeenCalledWith("session-1", "q1", "A) Option one");
    expect(api.getNextQuestion).toHaveBeenCalledTimes(2);
  });

  it("renders citations and verification labels in assistant messages", async () => {
    vi.mocked(api.getNextQuestion)
      .mockResolvedValueOnce({
        questionId: "q1",
        question: "Question 1",
        choices: ["Option one", "Option two", "Option three"],
        citations: [
          {
            title: "What is Azure AI Language?",
            url: "https://learn.microsoft.com/en-us/azure/ai-services/language-service/overview",
            retrievedAt: "2026-03-06"
          }
        ]
      })
      .mockResolvedValueOnce({
        questionId: "q2",
        question: "Question 2",
        choices: ["Next one", "Next two", "Next three"],
        citations: [
          {
            title: "Language service overview",
            url: "https://learn.microsoft.com/en-us/azure/ai-services/language-service/overview",
            retrievedAt: "2026-03-07"
          }
        ]
      });

    render(<App />);

    const startButton = screen.getByRole("button", { name: "Start Session" });
    await waitFor(() => expect(startButton).not.toBeDisabled());
    fireEvent.click(startButton);

    await screen.findByRole("button", { name: "A) Option one" });
    fireEvent.click(screen.getByRole("button", { name: "A) Option one" }));

    await screen.findByText("Verified from Learn MCP");
    expect(screen.getAllByRole("link", { name: /2026-03-06/ }).length).toBeGreaterThan(0);
    expect(screen.getByRole("link", { name: /2026-03-07/ })).toBeInTheDocument();
  });
});
