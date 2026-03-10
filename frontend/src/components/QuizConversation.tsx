import type { QuizQuestionResponse } from "../types";
import { formatCitation, withChoiceLabel } from "../quizUtils";
import type { ConversationEntry } from "../hooks/useQuizSessionFlow";

type QuizConversationProps = {
  conversation: ConversationEntry[];
  activeQuestion: QuizQuestionResponse | null;
  isAnswering: boolean;
  onAnswer: (choice: string) => void;
};

export function QuizConversation({ conversation, activeQuestion, isAnswering, onAnswer }: QuizConversationProps) {
  return (
    <section className="panel chat-panel">
      <h2>Quiz Chat</h2>

      <ul className="message-list">
        {conversation.map((entry) => (
          <li key={entry.id} className={`message-row ${entry.role === "user" ? "user" : "assistant"}`}>
            <article className="message-bubble">
              {entry.kind === "feedback" && (
                <p className={entry.correct ? "result-correct" : "result-incorrect"}>{entry.correct ? "Correct" : "Incorrect"}</p>
              )}
              <p>{entry.text}</p>
              {entry.memoryRule && <p className="memory-rule">{entry.memoryRule}</p>}
              {entry.verified && <p className="verified">Verified from Learn MCP</p>}
              {entry.warning && <p className="warning">{entry.warning}</p>}

              {(entry.citations?.length ?? 0) > 0 && (
                <ul className="citation-list">
                  {entry.citations!.map((citation) => (
                    <li key={`${citation.url}-${citation.retrievedAt}`}>
                      <a href={citation.url} target="_blank" rel="noreferrer">
                        {formatCitation(citation)}
                      </a>
                    </li>
                  ))}
                </ul>
              )}

              {entry.kind === "question" && activeQuestion?.questionId === entry.questionId && (
                <div className="choices">
                  {(activeQuestion.choices ?? []).map((choice, index) => {
                    const labeledChoice = withChoiceLabel(choice, index);
                    return (
                      <button key={labeledChoice} className="button-choice" disabled={isAnswering} onClick={() => onAnswer(labeledChoice)}>
                        {labeledChoice}
                      </button>
                    );
                  })}
                </div>
              )}
            </article>
          </li>
        ))}
      </ul>

      {isAnswering && <p>Thinking...</p>}
    </section>
  );
}
