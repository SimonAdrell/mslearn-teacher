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
        },
        {
          name: "Implement computer vision solutions",
          weightPercent: "15-20%",
          includes: ["Analyze images"]
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

  it("starts with area prompt in chat and no session setup controls", async () => {
    vi.mocked(api.getNextQuestion).mockResolvedValue({
      questionId: "q1",
      question: "Question 1",
      choices: ["Option one", "Option two", "Option three"],
      citations: []
    });

    render(<App />);

    expect(screen.queryByText("Session Setup")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Start Session" })).not.toBeInTheDocument();

    await screen.findByText("Let's start your AI-102 session. Pick a Skill Outline Area.");
    expect(screen.getByRole("button", { name: /Implement natural language processing solutions/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Implement computer vision solutions/ })).toBeInTheDocument();
  });

  it("selects area then mode, starts session automatically, and loads first question", async () => {
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

    const areaButton = await screen.findByRole("button", { name: /Implement natural language processing solutions/ });
    fireEvent.click(areaButton);

    await screen.findByText("Great. Choose your study mode.");
    fireEvent.click(screen.getByRole("button", { name: "Learn" }));

    await waitFor(() => {
      expect(api.startSession).toHaveBeenCalledWith("Learn", "Implement natural language processing solutions");
    });

    await screen.findByText("Question 1");
    expect(screen.getByRole("button", { name: "A) Option one" })).toBeInTheDocument();
  });

  it("keeps quiz flow with answer feedback, next question, citations, and verification", async () => {
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

    const areaButton = await screen.findByRole("button", { name: /Implement natural language processing solutions/ });
    fireEvent.click(areaButton);
    await screen.findByText("Great. Choose your study mode.");
    fireEvent.click(screen.getByRole("button", { name: "Learn" }));

    const optionButton = await screen.findByRole("button", { name: "A) Option one" });
    fireEvent.click(optionButton);

    await screen.findByText("Correct");
    await screen.findByText("Because");
    await screen.findByText("Rule");
    await screen.findByText("Question 2");
    expect(screen.getAllByText("Verified from Learn MCP").length).toBeGreaterThan(0);

    expect(api.submitAnswer).toHaveBeenCalledWith("session-1", "q1", "A) Option one");
    expect(api.getNextQuestion).toHaveBeenCalledTimes(2);
    expect(screen.getAllByRole("link", { name: /2026-03-06/ }).length).toBeGreaterThan(0);
    expect(screen.getByRole("link", { name: /2026-03-07/ })).toBeInTheDocument();
  });
});

