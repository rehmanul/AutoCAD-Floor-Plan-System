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
                'Content-Type': 'application/x-www-form-urlencoded',
                'Content-Length': Buffer.byteLength(postData)
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

async function createActivityVersion(token) {
    const activitySpec = {
        commandLine: [`$(engine.path)\\\\\\\\accoreconsole.exe /i "$(args[inputFile].path)" /al "$(appbundles[${FORGE_CLIENT_ID}.ProcessFloorPlanApp+$LATEST].path)" /s "PROCESS_FLOOR_PLAN\\n"`],
        parameters: {
            inputFile: {
                verb: 'get',
                description: 'Input DWG/DXF file',
                required: true,
                localName: 'input.dwg'
            },
            settingsFile: {
                verb: 'get',
                description: 'Processing settings JSON',
                required: true,
                localName: 'settings.json'
            },
            finalPlanDwg: {
                verb: 'put',
                description: 'Output final plan DWG',
                required: true,
                localName: 'final_plan.dwg'
            },
            finalPlanPng: {
                verb: 'put',
                description: 'Output final plan PNG',
                required: true,
                localName: 'final_plan.png'
            },
            measurements: {
                verb: 'put',
                description: 'Measurements JSON output',
                required: true,
                localName: 'measurements.json'
            }
        },
        engine: 'Autodesk.AutoCAD+25_1',
        appbundles: [`${FORGE_CLIENT_ID}.ProcessFloorPlanApp+$LATEST`],
        description: 'Process floor plan and generate ilots with corridors'
    };

    return new Promise((resolve, reject) => {
        const json = JSON.stringify(activitySpec);
        console.log('Creating activity version with payload:', json);
        
        const options = {
            hostname: 'developer.api.autodesk.com',
            path: `/da/us-east/v3/activities/${FORGE_CLIENT_ID}.ProcessFloorPlanActivity/versions`,
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json',
                'Content-Length': Buffer.byteLength(json)
            }
        };

        const req = https.request(options, (res) => {
            let data = '';
            res.on('data', (chunk) => data += chunk);
            res.on('end', () => {
                console.log('Response status:', res.statusCode);
                console.log('Response:', data);
                if (res.statusCode >= 200 && res.statusCode < 300) {
                    const response = JSON.parse(data);
                    console.log('Activity version created:', response.version);
                    resolve(response.version);
                } else {
                    reject(new Error(`Failed to create version: ${data}`));
                }
            });
        });

        req.on('error', reject);
        req.write(json);
        req.end();
    });
}

async function main() {
    try {
        const token = await getAccessToken();
        const version = await createActivityVersion(token);
        console.log(`\nUse this activity ID in your API: ${FORGE_CLIENT_ID}.ProcessFloorPlanActivity+${version}`);
    } catch (error) {
        console.error('Error:', error.message);
    }
}

main();