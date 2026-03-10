import type { SkillArea } from "../types";
import { MODES, ONBOARDING_STEPS } from "../quizUtils";

type OnboardingFlowProps = {
  onboardingStep: number;
  onboardingProgressValue: number;
  mode: string;
  skillArea: string;
  areas: SkillArea[];
  canStart: boolean;
  onModeChange: (mode: string) => void;
  onSkillAreaChange: (skillArea: string) => void;
  onBack: () => void;
  onPrevious: () => void;
  onNext: () => void;
  onStartSession: () => void;
};

export function OnboardingFlow({
  onboardingStep,
  onboardingProgressValue,
  mode,
  skillArea,
  areas,
  canStart,
  onModeChange,
  onSkillAreaChange,
  onBack,
  onPrevious,
  onNext,
  onStartSession
}: OnboardingFlowProps) {
  return (
    <section className="panel onboarding-panel" aria-labelledby="onboarding-title">
      <p className="eyebrow">Onboarding</p>
      <h1 id="onboarding-title">Set up your next study session</h1>
      <header className="progress-header">
        <p className="progress-copy">
          Step {onboardingStep} of {ONBOARDING_STEPS}
        </p>
        <div
          className="progress-track"
          role="progressbar"
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={onboardingProgressValue}
          aria-label="Onboarding progress"
        >
          <div className="progress-fill" style={{ width: `${onboardingProgressValue}%` }} />
        </div>
      </header>

      {onboardingStep === 1 && (
        <div className="step-panel">
          <h2>Choose your study mode</h2>
          <p className="hint">Pick the coaching style you want for this session.</p>
          <label htmlFor="mode-select" className="field-label">
            Mode
          </label>
          <select id="mode-select" value={mode} onChange={(e) => onModeChange(e.target.value)}>
            {MODES.map((item) => (
              <option key={item} value={item}>
                {item}
              </option>
            ))}
          </select>
        </div>
      )}

      {onboardingStep === 2 && (
        <div className="step-panel">
          <h2>Select your skill outline focus</h2>
          <p className="hint">We will anchor questions to this AI-102 objective area.</p>
          <label htmlFor="skill-area-select" className="field-label">
            Skill outline area
          </label>
          <select id="skill-area-select" value={skillArea} onChange={(e) => onSkillAreaChange(e.target.value)}>
            {areas.map((area) => (
              <option key={area.name} value={area.name}>
                {area.name} ({area.weightPercent})
              </option>
            ))}
          </select>
        </div>
      )}

      <div className="button-row">
        <button className="button-secondary" onClick={onboardingStep === 1 ? onBack : onPrevious}>
          {onboardingStep === 1 ? "Back" : "Previous"}
        </button>

        {onboardingStep < ONBOARDING_STEPS ? (
          <button className="button-primary" onClick={onNext}>
            Next
          </button>
        ) : (
          <button className="button-primary" disabled={!canStart} onClick={onStartSession}>
            Start Session
          </button>
        )}
      </div>
    </section>
  );
}
