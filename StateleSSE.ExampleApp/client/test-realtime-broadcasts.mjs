import * as signalR from '@microsoft/signalr';

const HUB_URL = 'http://localhost:5000/quizhub';

console.log('🧪 Testing Realtime Broadcasts\n');

const broadcastsReceived = { conn1: [], conn2: [] };

async function getAuthToken() {
  // Register a test user to get a JWT token
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL, {
      skipNegotiation: true,
      transport: signalR.HttpTransportType.WebSockets
    })
    .configureLogging(signalR.LogLevel.Error)
    .build();

  await connection.start();
  console.log('📌 Registering test user...');

  const username = 'test_' + Date.now();
  const response = await connection.invoke('Register', {
    Name: username,
    Password: 'test123'
  });

  await connection.stop();
  console.log(`✅ Registered as ${username}, got token\n`);
  return response.token;
}

async function createConnection(name, token) {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL, {
      accessTokenFactory: () => token,
      skipNegotiation: true,
      transport: signalR.HttpTransportType.WebSockets
    })
    .configureLogging(signalR.LogLevel.Error)
    .build();

  connection.on('OnBroadcast', (data) => {
    console.log(`📡 ${name} RECEIVED BROADCAST!`);
    console.log(`   Type: ${Array.isArray(data) ? `Array[${data.length}]` : typeof data}`);
    broadcastsReceived[name].push(data);
  });

  await connection.start();
  console.log(`✅ ${name} connected`);
  return connection;
}

(async () => {
  let conn1, conn2;
  try {
    // Get auth token
    const TOKEN = await getAuthToken();

    // Connect
    console.log('📌 Creating connections...\n');
    conn1 = await createConnection('conn1', TOKEN);
    conn2 = await createConnection('conn2', TOKEN);

    // Subscribe to realtime
    console.log('\n📌 Calling ListQuizzesRealtime({ enabled: true, serverPush: true })...\n');
    const opts = { enabled: true, serverPush: true };

    const res1 = await conn1.invoke('ListQuizzesRealtime', opts);
    console.log('conn1:', JSON.stringify(res1).substring(0, 200));

    const res2 = await conn2.invoke('ListQuizzesRealtime', opts);
    console.log('conn2:', JSON.stringify(res2).substring(0, 200));

    // Get or create quiz
    let quizId = res1?.data?.[0]?.id;
    if (!quizId) {
      console.log('\n📌 No quizzes found, creating one...\n');
      const quiz = await conn1.invoke('CreateQuiz', {
        name: 'Test ' + Date.now(),
        questions: [{
          questiontext: 'Q?',
          answers: [
            { answertext: 'A', iscorrect: true },
            { answertext: 'B', iscorrect: false }
          ]
        }]
      });
      quizId = quiz.id;
    }

    console.log('\n📌 Waiting 1s for subscriptions...');
    await new Promise(r => setTimeout(r, 1000));

    // Update quiz to trigger broadcasts
    console.log(`\n📌 Calling UpdateQuiz("${quizId}")...\n`);
    await conn1.invoke('UpdateQuiz', quizId);

    console.log('📌 Waiting 3s for broadcasts...');
    await new Promise(r => setTimeout(r, 3000));

    // Check results
    console.log('\n📊 RESULTS:');
    console.log(`   conn1 broadcasts: ${broadcastsReceived.conn1.length}`);
    console.log(`   conn2 broadcasts: ${broadcastsReceived.conn2.length}`);

    if (broadcastsReceived.conn1.length > 0 && broadcastsReceived.conn2.length > 0) {
      console.log('\n✅ ✅ ✅ SUCCESS! Both connections received broadcasts!\n');
      process.exit(0);
    } else {
      console.log('\n❌ FAILURE - No broadcasts received');
      console.log('\nServer should log:');
      console.log('  ✅ Registered reactive query: Method=ListQuizzesRealtime');
      console.log('  Found 2 reactive query subscriptions for Quiz');
      console.log('  📡 Pushed reactive query result to connection...\n');
      process.exit(1);
    }
  } catch (err) {
    console.error('\n❌ Error:', err.message);
    process.exit(1);
  } finally {
    if (conn1) await conn1.stop();
    if (conn2) await conn2.stop();
  }
})();
