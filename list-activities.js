const https = require('https');

const FORGE_CLIENT_ID = 'bZCKOFynve2w4rpzNYmooBYAGuqxKWelBTiGcfdoSUpVlD0r';
const FORGE_CLIENT_SECRET = 'QusNbDYeB6WFl9vzDSq16Gcpbz7rJO2tIMcJBTBV0ro0GRrS2O9s4gRPzT1uVSoS';

async function getAccessToken() {
    return new Promise((resolve, reject) => {
        const postData = `client_id=${FORGE_CLIENT_ID}&client_secret=${FORGE_CLIENT_SECRET}&grant_type=client_credentials&scope=code:all`;
        
        const options = {
            hostname: 'developer.api.autodesk.com',
            path: '/authentication/v2/token',
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            }
        };

        const req = https.request(options, (res) => {
            let data = '';
            res.on('data', (chunk) => data += chunk);
            res.on('end', () => {
                const response = JSON.parse(data);
                resolve(response.access_token);
            });
        });

        req.write(postData);
        req.end();
    });
}

async function listActivities(token) {
    return new Promise((resolve, reject) => {
        const options = {
            hostname: 'developer.api.autodesk.com',
            path: '/da/us-east/v3/activities',
            method: 'GET',
            headers: {
                'Authorization': `Bearer ${token}`
            }
        };

        const req = https.request(options, (res) => {
            let data = '';
            res.on('data', (chunk) => data += chunk);
            res.on('end', () => {
                console.log(`Status: ${res.statusCode}`);
                console.log(`Activities: ${data}`);
                resolve(data);
            });
        });

        req.end();
    });
}

async function main() {
    try {
        const token = await getAccessToken();
        await listActivities(token);
    } catch (error) {
        console.error('Error:', error.message);
    }
}

main();