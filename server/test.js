const axios = require('axios');

// Test script to verify server is working
const SERVER_URL = 'http://localhost:3000';

async function testServer() {
    console.log('🧪 Testing CS2 Overlay Server...\n');
    
    try {
        // Test 1: Check server status
        console.log('1. Checking server status...');
        const statusResponse = await axios.get(`${SERVER_URL}/status`);
        console.log('✅ Status:', statusResponse.data);
        console.log('');
        
        // Test 2: Get match data
        console.log('2. Getting match data...');
        const gsiResponse = await axios.get(`${SERVER_URL}/gsi`);
        console.log('✅ Match data:', gsiResponse.data);
        console.log('');
        
        // Test 3: Update URL (example)
        console.log('3. Testing URL update...');
        const urlResponse = await axios.post(`${SERVER_URL}/url`, {
            url: 'https://www.hltv.org/matches'
        });
        console.log('✅ URL update:', urlResponse.data);
        console.log('');
        
        // Test 4: Update selectors
        console.log('4. Testing selector update...');
        const selectorResponse = await axios.post(`${SERVER_URL}/selectors`, {
            selectors: {
                team1Name: '.team1 .name',
                team2Name: '.team2 .name'
            }
        });
        console.log('✅ Selector update:', selectorResponse.data);
        console.log('');
        
        console.log('🎉 All tests passed! Server is working correctly.');
        
    } catch (error) {
        console.error('❌ Test failed:', error.message);
        if (error.response) {
            console.error('Response:', error.response.data);
        }
    }
}

// Run tests if server is not responding
async function waitForServer() {
    console.log('⏳ Waiting for server to start...');
    let attempts = 0;
    const maxAttempts = 30;
    
    while (attempts < maxAttempts) {
        try {
            await axios.get(`${SERVER_URL}/status`);
            console.log('✅ Server is ready!');
            return true;
        } catch (error) {
            attempts++;
            console.log(`⏳ Attempt ${attempts}/${maxAttempts}...`);
            await new Promise(resolve => setTimeout(resolve, 1000));
        }
    }
    
    console.error('❌ Server did not respond after 30 seconds');
    return false;
}

async function main() {
    const serverReady = await waitForServer();
    if (serverReady) {
        await testServer();
    }
}

if (require.main === module) {
    main();
}

module.exports = { testServer, waitForServer };