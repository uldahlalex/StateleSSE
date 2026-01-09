import {useEffect, useState} from 'react';
import {useUser} from "../contexts/UserContext.tsx";
import {ApiClient} from "../utils/ApiClient.ts";
import {ensureDefined, validateRequest, ValidationException} from "../utils/validation.ts";
import type {
    CurrentRoundInfo, GameEventUnion,
    GameStateResponse,
    QuestionOptionInfo,
    QuizReturnDto,
    RoundStartedEvent
} from "../generated-client.ts";
import {createTypedEventStream, streamRoundStarted} from "../generated-sse-client.ts";


export default function KahootGameExample() {
    const [mode, setMode] = useState<'create' | 'join' | null>(null);
    const {user, setUser} = useUser();

    if (!mode) {
        return (
            <div className="min-h-screen flex items-center justify-center bg-base-200">
                <div className="card w-96 bg-base-100 shadow-xl">
                    <div className="card-body">
                        <h2 className="card-title">Kahoot Game Demo</h2>
                        <p className="text-sm text-gray-500">SSE + REST + Redis + EF Core</p>

                        <div className="space-y-3 mt-4">
                            <button
                                onClick={() => setMode('create')}
                                className="btn btn-primary w-full"
                            >
                                Create & Host Game
                            </button>
                            <button
                                onClick={() => setMode('join')}
                                className="btn btn-secondary w-full"
                            >
                                Join Game
                            </button>
                        </div>

                        <div className="divider">Architecture</div>
                        <div className="text-sm space-y-1">
                            <p><strong>Database:</strong> EF Core + PostgreSQL</p>
                            <p><strong>Realtime:</strong> Generic RedisBackplane</p>
                            <p><strong>Pattern:</strong> SSE for events, REST for actions</p>
                            <p><strong>User ID:</strong> {user?.id}</p>
                        </div>
                    </div>
                </div>
               
            </div>
        );
    }

    return mode === 'create' ? <HostView userId={user?.id!} /> : <PlayerView userId={user?.id!} />;
}

function HostView({ userId }: { userId: string }) {
    const [quizzes, setQuizzes] = useState<QuizReturnDto[]>([]);
    const [selectedQuizId, setSelectedQuizId] = useState<string>('');
    const [gameId, setGameId] = useState<string | null>(null);
    const [gameState, setGameState] = useState<GameStateResponse | null>(null);
    const [events, setEvents] = useState<GameEventUnion[]>([]);
    const [questionIdInput, setQuestionIdInput] = useState<string>('');
    const [error, setError] = useState<string | null>(null);

    // Load available quizzes (you'd need to add this endpoint)
    useEffect(() => {
            ApiClient.getQuizzes().then(r => {
                setQuizzes(r)
            })
          }, []);

    // Subscribe to game events
    useEffect(() => {
        if (!gameId) return;
        const es = streamRoundStarted('game-123');
        createTypedEventStream<RoundStartedEvent>(
            es.url,
            (event) => {
                console.log(event.questionText); // Fully typed!
            }
        );

 
    }, [gameId]);

    const createGame = async () => {
        try {
            setError(null);
            if (!selectedQuizId) return;

            const data = await ApiClient.createGame(
                validateRequest({
                    quizId: selectedQuizId,
                    hostUserId: userId
                }, ['quizId', 'hostUserId'])
            );

            setGameId(ensureDefined(data.gameId, 'gameId'));
        } catch (e) {
            const message = e instanceof ValidationException
                ? e.message
                : 'Failed to create game';
            setError(message);
            console.error('Create game error:', e);
        }
    };

    const startRound = async () => {
        if (!gameId || !questionIdInput) return;

        await ApiClient.startRound(
            validateRequest({
                gameId,
                questionId: questionIdInput
            }, ['gameId', 'questionId'])
        );

        setQuestionIdInput('');
    };

    const endRound = async () => {
        if (!gameId || !gameState?.currentRound) return;

        await ApiClient.endRound(
            validateRequest({
                gameId,
                roundId: ensureDefined(gameState.currentRound.roundId, 'roundId')
            }, ['gameId', 'roundId'])
        );
    };

    if (!gameId) {
        return (
            <div className="min-h-screen bg-base-200 p-8">
                <div className="max-w-2xl mx-auto">
                    <h1 className="text-3xl font-bold mb-6">Create Game</h1>

                    <div className="card bg-base-100 shadow-xl">
                        <div className="card-body">
                            <h2 className="card-title">Select Quiz</h2>

                            <select
                                className="select select-bordered w-full"
                                value={selectedQuizId}
                                onChange={(e) => setSelectedQuizId(e.target.value)}
                            >
                                <option value="">Choose a quiz...</option>
                                {quizzes.map(quiz => (
                                    <option key={quiz.id} value={quiz.id}>
                                        {quiz.name} ({quiz.totalQuestions} questions)
                                    </option>
                                ))}
                            </select>

                            {error && (
                                <div className="alert alert-error mt-4">
                                    <p>{error}</p>
                                </div>
                            )}

                            <button
                                onClick={createGame}
                                disabled={!selectedQuizId}
                                className="btn btn-primary mt-4"
                            >
                                Create Game
                            </button>

                            {quizzes.length === 0 && (
                                <div className="alert alert-info mt-4">
                                    <p>No quizzes available. Create a quiz first or seed the database.</p>
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-base-200 p-8">
            <div className="max-w-6xl mx-auto">
                <div className="flex justify-between items-center mb-6">
                    <h1 className="text-3xl font-bold">Host Dashboard</h1>
                    <div className="badge badge-primary">Game ID: {gameId}</div>
                </div>

                {/* Controls */}
                <div className="card bg-base-100 shadow-xl mb-6">
                    <div className="card-body">
                        <h2 className="card-title">Controls</h2>
                        <div className="flex gap-2 mb-4">
                            <input
                                type="text"
                                placeholder="Enter Question ID"
                                className="input input-bordered flex-1"
                                value={questionIdInput}
                                onChange={(e) => setQuestionIdInput(e.target.value)}
                                disabled={gameState?.currentRound !== null}
                            />
                            <button
                                onClick={startRound}
                                className="btn btn-success"
                                disabled={gameState?.currentRound !== null || !questionIdInput}
                            >
                                Start Round
                            </button>
                            <button
                                onClick={endRound}
                                className="btn btn-warning"
                                disabled={!gameState?.currentRound}
                            >
                                End Round & Show Results
                            </button>
                        </div>
                        <p className="text-sm text-gray-500">
                            Players must join before starting the first round
                        </p>
                    </div>
                </div>

                {/* Game State */}
                {gameState && (
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-6">
                        {/* Players */}
                        <div className="card bg-base-100 shadow-xl">
                            <div className="card-body">
                                <h2 className="card-title">Players ({gameState.players?.length || 0})</h2>
                                <div className="space-y-2">
                                    {gameState.players?.map(player => (
                                        <div key={player.userId} className="badge badge-lg">
                                            {player.userName}
                                        </div>
                                    ))}
                                </div>
                            </div>
                        </div>

                        {/* Leaderboard */}
                        <div className="card bg-base-100 shadow-xl">
                            <div className="card-body">
                                <h2 className="card-title">Leaderboard</h2>
                                <table className="table table-sm">
                                    <thead>
                                        <tr>
                                            <th>Rank</th>
                                            <th>Player</th>
                                            <th>Score</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {gameState.leaderboard?.map((entry, i) => (
                                            <tr key={entry.userId}>
                                                <td>{i + 1}</td>
                                                <td>{entry.userName}</td>
                                                <td className="font-bold">{entry.score}</td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    </div>
                )}

                {/* Current Round */}
                {gameState?.currentRound && (
                    <div className="card bg-base-100 shadow-xl mb-6">
                        <div className="card-body">
                            <h2 className="card-title">Current Round</h2>
                            <p className="text-xl">{gameState.currentRound.questionText}</p>
                            <div className="grid grid-cols-2 gap-4 mt-4">
                                {gameState.currentRound.options?.map(opt => (
                                    <div key={opt.id} className="btn btn-outline">
                                        {opt.text}
                                    </div>
                                ))}
                            </div>
                        </div>
                    </div>
                )}

                {/* Event Log */}
                <div className="card bg-base-100 shadow-xl">
                    <div className="card-body">
                        <h2 className="card-title">Event Stream</h2>
                        <div className="max-h-64 overflow-y-auto space-y-2">
                            {events.slice(-10).reverse().map((event, i) => (
                                <div key={i} className="alert alert-sm">
                                    <code className="text-xs">{event.type}</code>
                                </div>
                            ))}
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}

function PlayerView({ userId }: { userId: string }) {
    const [gameId, setGameId] = useState('');
    const [hasJoined, setHasJoined] = useState(false);
    const [gameState, setGameState] = useState<GameStateResponse | null>(null);
    const [currentRound, setCurrentRound] = useState<CurrentRoundInfo | null>(null);
    const [hasAnswered, setHasAnswered] = useState(false);
    const [myAnswer, setMyAnswer] = useState<{ optionId: string; isCorrect: boolean } | null>(null);

    useEffect(() => {
        if (!hasJoined || !gameId) return;


        }, [hasJoined, gameId]);

    const joinGame = async () => {
        const res = await ApiClient.joinGame(
            validateRequest({
                gameId,
                userId
            }, ['gameId', 'userId'])
        );

        if (res.status === 'success') {
            setHasJoined(true);
        }
    };

    const submitAnswer = async (optionId: string) => {
        if (!currentRound || hasAnswered) return;

        const data = await ApiClient.submitAnswer(
            validateRequest({
                gameId,
                roundId: ensureDefined(currentRound.roundId, 'roundId'),
                userId,
                optionId
            }, ['gameId', 'roundId', 'userId', 'optionId'])
        );

        setMyAnswer({ optionId, isCorrect: ensureDefined(data.isCorrect, 'isCorrect') });
        setHasAnswered(true);
    };

    if (!hasJoined) {
        return (
            <div className="min-h-screen flex items-center justify-center bg-base-200">
                <div className="card w-96 bg-base-100 shadow-xl">
                    <div className="card-body">
                        <h2 className="card-title">Join Game</h2>
                        <input
                            type="text"
                            placeholder="Enter Game ID"
                            className="input input-bordered"
                            value={gameId}
                            onChange={(e) => setGameId(e.target.value)}
                        />
                        <button
                            onClick={joinGame}
                            disabled={!gameId}
                            className="btn btn-primary"
                        >
                            Join Game
                        </button>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-base-200 p-8">
            <div className="max-w-4xl mx-auto">
                <div className="flex justify-between items-center mb-6">
                    <h1 className="text-3xl font-bold">Player View</h1>
                    <div className="badge badge-secondary">User ID: {userId}</div>
                </div>

                {/* Question */}
                {currentRound && (
                    <div className="card bg-base-100 shadow-xl mb-6">
                        <div className="card-body">
                            <h2 className="card-title">Question</h2>
                            <p className="text-2xl font-bold mb-4">{currentRound.questionText}</p>

                            <div className="grid grid-cols-2 gap-4">
                                {currentRound.options?.map((opt: QuestionOptionInfo) => (
                                    <button
                                        key={opt.id}
                                        onClick={() => submitAnswer(opt.id!)}
                                        disabled={hasAnswered}
                                        className={`btn btn-lg ${
                                            myAnswer?.optionId === opt.id
                                                ? myAnswer?.isCorrect
                                                    ? 'btn-success'
                                                    : 'btn-error'
                                                : 'btn-primary'
                                        }`}
                                    >
                                        {opt.text}
                                    </button>
                                ))}
                            </div>

                            {myAnswer && (
                                <div className={`alert ${myAnswer.isCorrect ? 'alert-success' : 'alert-error'} mt-4`}>
                                    {myAnswer.isCorrect ? '✓ Correct!' : '✗ Wrong answer'}
                                </div>
                            )}
                        </div>
                    </div>
                )}

                {!currentRound && (
                    <div className="alert alert-info">
                        Waiting for host to start the next round...
                    </div>
                )}

                {/* Leaderboard */}
                {gameState?.leaderboard && gameState.leaderboard.length > 0 && (
                    <div className="card bg-base-100 shadow-xl">
                        <div className="card-body">
                            <h2 className="card-title">Leaderboard</h2>
                            <table className="table">
                                <thead>
                                    <tr>
                                        <th>Rank</th>
                                        <th>Player</th>
                                        <th>Score</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {gameState.leaderboard.map((entry, i) => (
                                        <tr
                                            key={entry.userId}
                                            className={entry.userId === userId ? 'bg-primary text-primary-content' : ''}
                                        >
                                            <td>{i + 1}</td>
                                            <td>{entry.userName}</td>
                                            <td className="font-bold">{entry.score}</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
}
