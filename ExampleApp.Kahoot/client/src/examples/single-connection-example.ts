import { BASE_URL } from '../utils/BASE_URL';
import type {
    RoundStartedEvent,
    PlayerJoinedEvent,
    AnswerSubmittedEvent,
    RoundEndedEvent,
    GameCreatedEvent
} from '../generated-client';

export function singleConnectionExample(gameId: string) {
    const url = `${BASE_URL}/all?gameId=${gameId}`;
    const es = new EventSource(url);

    es.addEventListener('RoundStartedEvent', (e) => {
        const data: RoundStartedEvent = JSON.parse((e as MessageEvent).data);
        console.log('Round started:', data.questionText);
        console.log('Time limit:', data.timeLimit);
        console.log('Options:', data.options);
    });

    es.addEventListener('PlayerJoinedEvent', (e) => {
        const data: PlayerJoinedEvent = JSON.parse((e as MessageEvent).data);
        console.log('Player joined:', data.userName);
        console.log('Total players:', data.playerCount);
    });

    es.addEventListener('AnswerSubmittedEvent', (e) => {
        const data: AnswerSubmittedEvent = JSON.parse((e as MessageEvent).data);
        console.log('Answer submitted by:', data.userId);
        console.log('Answers received:', data.answersReceived);
    });

    es.addEventListener('RoundEndedEvent', (e) => {
        const data: RoundEndedEvent = JSON.parse((e as MessageEvent).data);
        console.log('Round ended. Correct answer:', data.correctOptionId);
        console.log('Results:', data.results);
        console.log('Leaderboard:', data.leaderboard);
    });

    es.addEventListener('GameCreatedEvent', (e) => {
        const data: GameCreatedEvent = JSON.parse((e as MessageEvent).data);
        console.log('Game created:', data.quizName);
        console.log('Question count:', data.questionCount);
    });

    es.onerror = (error) => {
        console.error('SSE error:', error);
    };

    return {
        close: () => es.close()
    };
}

export function comparisonOldVsNew() {
    const gameId = 'game-123';

    console.log('OLD APPROACH: 5 connections');
    const stream1 = new EventSource(`${BASE_URL}/RoundStartedEvent?gameId=${gameId}`);
    const stream2 = new EventSource(`${BASE_URL}/PlayerJoinedEvent?gameId=${gameId}`);
    const stream3 = new EventSource(`${BASE_URL}/AnswerSubmittedEvent?gameId=${gameId}`);
    const stream4 = new EventSource(`${BASE_URL}/RoundEndedEvent?gameId=${gameId}`);
    const stream5 = new EventSource(`${BASE_URL}/GameCreatedEvent?gameId=${gameId}`);

    console.log('NEW APPROACH: 1 connection');
    const singleStream = new EventSource(`${BASE_URL}/all?gameId=${gameId}`);

    singleStream.addEventListener('RoundStartedEvent', (e) => {
        console.log('Round started');
    });
    singleStream.addEventListener('PlayerJoinedEvent', (e) => {
        console.log('Player joined');
    });

    return {
        closeOld: () => {
            stream1.close();
            stream2.close();
            stream3.close();
            stream4.close();
            stream5.close();
        },
        closeNew: () => {
            singleStream.close();
        }
    };
}
