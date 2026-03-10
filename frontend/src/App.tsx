import { OnboardingFlow } from "./components/OnboardingFlow";
import { QuizConversation } from "./components/QuizConversation";
import { SessionHeader } from "./components/SessionHeader";
import { StartPage } from "./components/StartPage";
import { useQuizSessionFlow } from "./hooks/useQuizSessionFlow";

export function App() {
  const flow = useQuizSessionFlow();

  return (
    <main className="layout">
      {flow.phase === "start" && <StartPage onBegin={flow.goToOnboarding} />}

      {flow.phase === "onboarding" && (
        <OnboardingFlow
          onboardingStep={flow.onboardingStep}
          onboardingProgressValue={flow.onboardingProgressValue}
          mode={flow.mode}
          skillArea={flow.skillArea}
          areas={flow.areas}
          canStart={flow.canStart}
          onModeChange={flow.setMode}
          onSkillAreaChange={flow.setSkillArea}
          onBack={flow.backToStart}
          onPrevious={flow.previousOnboardingStep}
          onNext={flow.nextOnboardingStep}
          onStartSession={() => {
            void flow.onStartSession();
          }}
        />
      )}

      {flow.phase === "session" && (
        <>
          <SessionHeader mode={flow.mode} skillArea={flow.skillArea} questionNumber={flow.questionNumber} sessionId={flow.sessionId} />
          <QuizConversation
            conversation={flow.conversation}
            activeQuestion={flow.activeQuestion}
            isAnswering={flow.isAnswering}
            onAnswer={(choice) => {
              void flow.onAnswer(choice);
            }}
          />
        </>
      )}

      {flow.error && <p className="error">{flow.error}</p>}
    </main>
  );
}
