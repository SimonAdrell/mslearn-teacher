import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { App } from "./App";
import * as api from "./api";

vi.mock("./api", async () => {
  const actual = await vi.importActual<typeof import("./api")>("./api");

  return {
    ...actual,
    bootstrapSession: vi.fn(),
    configureSession: vi.fn(),
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
    window.history.pushState(null, "", "/");
  });

  beforeEach(() => {
    vi.resetAllMocks();
    window.history.pushState(null, "", "/");

    vi.mocked(api.bootstrapSession).mockResolvedValue({
      sessionId: "session-boot-1",
      message: "Let's start your AI-102 session. Pick a Skill Outline Area.",
      areaOptions: [
        "Implement natural language processing solutions (30-35%)",
        "Implement computer vision solutions (15-20%)"
      ],
      modeOptions: ["Learn", "Quiz", "Review mistakes", "Rapid cram"]
    });

    vi.mocked(api.configureSession).mockResolvedValue(undefined);

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

  it("renders start page with instructions and start learning button", () => {
    render(<App />);

    expect(screen.getByRole("heading", { name: /Study with a guided start/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Start learning" })).toBeInTheDocument();
    expect(api.bootstrapSession).not.toHaveBeenCalled();
  });

  it("navigates to onboarding and loads bootstrap options", async () => {
    render(<App />);

    fireEvent.click(screen.getByRole("button", { name: "Start learning" }));

    await screen.findByText("Let's start your AI-102 session. Pick a Skill Outline Area.");

    expect(api.bootstrapSession).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("button", { name: /Implement natural language processing solutions/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Implement computer vision solutions/ })).toBeInTheDocument();
  });

  it("selects area then mode, configures session, and loads first question", async () => {
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
    fireEvent.click(screen.getByRole("button", { name: "Start learning" }));

    const areaButton = await screen.findByRole("button", { name: /Implement natural language processing solutions/ });
    fireEvent.click(areaButton);

    const modeButton = await screen.findByRole("button", { name: "Learn" });
    fireEvent.click(modeButton);

    await waitFor(() => {
      expect(api.configureSession).toHaveBeenCalledWith(
        "session-boot-1",
        "Learn",
        "Implement natural language processing solutions (30-35%)"
      );
    });

    await screen.findByText("Question 1");
    expect(screen.getByRole("heading", { name: "AI-102 Study Coach" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "A) Option one" })).toBeInTheDocument();
    expect(screen.getByText(/Area: Implement natural language processing solutions/)).toBeInTheDocument();
    expect(screen.getByText("Mode: Learn")).toBeInTheDocument();
  });

  it("keeps feedback and auto-next behavior with citations", async () => {
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
    fireEvent.click(screen.getByRole("button", { name: "Start learning" }));

    const areaButton = await screen.findByRole("button", { name: /Implement natural language processing solutions/ });
    fireEvent.click(areaButton);

    const modeButton = await screen.findByRole("button", { name: "Learn" });
    fireEvent.click(modeButton);

    const optionButton = await screen.findByRole("button", { name: "A) Option one" });
    fireEvent.click(optionButton);

    await screen.findByText("Correct");
    await screen.findByText("Because");
    await screen.findByText("Rule");
    await screen.findByText("Question 2");

    expect(api.submitAnswer).toHaveBeenCalledWith("session-boot-1", "q1", "A) Option one");
    expect(api.getNextQuestion).toHaveBeenCalledTimes(2);
    expect(screen.getAllByText("Verified from Learn MCP").length).toBeGreaterThan(0);
    expect(screen.getAllByRole("link", { name: /2026-03-06/ }).length).toBeGreaterThan(0);
    expect(screen.getByRole("link", { name: /2026-03-07/ })).toBeInTheDocument();
  });
});
