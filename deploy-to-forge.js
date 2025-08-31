const fs = require('fs');
const path = require('path');
const https = require('https');
const FormData = require('form-data');

const FORGE_CLIENT_ID = 'bZCKOFynve2w4rpzNYmooBYAGuqxKWelBTiGcfdoSUpVlD0r';
const FORGE_CLIENT_SECRET = 'QusNbDYeB6WFl9vzDSq16Gcpbz7rJO2tIMcJBTBV0ro0GRrS2O9s4gRPzT1uVSoS';
const BASE_URL = 'https://developer.api.autodesk.com/da/us-east/v3';

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
                try {
                    const response = JSON.parse(data);
                    resolve(response.access_token);
                } catch (error) {
                    reject(error);
                }
            });
        });

        req.on('error', reject);
        req.write(postData);
        req.end();
    });
}

async function uploadAppBundle(token) {
    const bundlePath = path.join(__dirname, 'src', 'AutoCADPlugin', 'FloorPlanProcessor.zip');
    
    if (!fs.existsSync(bundlePath)) {
        throw new Error('Bundle file not found. Run deploy-plugin.bat first.');
    }

    const appBundleSpec = {
        id: 'ProcessFloorPlanApp',
        engine: 'Autodesk.AutoCAD+25_1',
        description: 'Floor Plan Processing Application'
    };

    try {
        // Try to create app bundle
        const createResponse = await makeRequest('POST', '/appbundles', token, appBundleSpec);
        console.log('App bundle created:', createResponse.id);

        // Upload bundle
        const uploadUrl = createResponse.uploadParameters.endpointURL;
        const formData = createResponse.uploadParameters.formData;
        
        await uploadFile(uploadUrl, formData, bundlePath);
        console.log('Bundle uploaded successfully');

        return createResponse.id;
    } catch (error) {
        if (error.message.includes('409')) {
            console.log('App bundle already exists, using existing one');
            return `${FORGE_CLIENT_ID}.ProcessFloorPlanApp+LATEST`;
        }
        throw error;
    }
}

async function createActivity(token, appBundleId) {
    const activitySpec = {
        id: 'ProcessFloorPlanActivity',
        commandLine: [`$(engine.path)\\\\accoreconsole.exe /i "$(args[inputFile].path)" /al "$(appbundles[${appBundleId}].path)" /s "PROCESS_FLOOR_PLAN\n"`],
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
        appbundles: [appBundleId],
        description: 'Process floor plan and generate ilots with corridors'
    };

    try {
        const response = await makeRequest('POST', '/activities', token, activitySpec);
        console.log('Activity created:', response.id);
        return response.id;
    } catch (error) {
        if (error.message.includes('409')) {
            console.log('Activity already exists, using existing one');
            return `${FORGE_CLIENT_ID}.ProcessFloorPlanActivity+LATEST`;
        }
        throw error;
    }
}

async function makeRequest(method, endpoint, token, data = null) {
    return new Promise((resolve, reject) => {
        const options = {
            hostname: 'developer.api.autodesk.com',
            path: `/da/us-east/v3${endpoint}`,
            method: method,
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        };

        const req = https.request(options, (res) => {
            let responseData = '';
            res.on('data', (chunk) => responseData += chunk);
            res.on('end', () => {
                try {
                    const response = JSON.parse(responseData);
                    if (res.statusCode >= 200 && res.statusCode < 300) {
                        resolve(response);
                    } else {
                        reject(new Error(`HTTP ${res.statusCode}: ${response.detail || responseData}`));
                    }
                } catch (error) {
                    reject(error);
                }
            });
        });

        req.on('error', reject);
        
        if (data) {
            req.write(JSON.stringify(data));
        }
        req.end();
    });
}

async function uploadFile(url, formData, filePath) {
    return new Promise((resolve, reject) => {
        const form = new FormData();
        
        // Add form data fields
        Object.keys(formData).forEach(key => {
            form.append(key, formData[key]);
        });
        
        // Add file
        form.append('file', fs.createReadStream(filePath));
        
        form.submit(url, (err, res) => {
            if (err) {
                reject(err);
            } else {
                resolve(res);
            }
        });
    });
}

async function main() {
    try {
        console.log('Getting access token...');
        const token = await getAccessToken();
        
        console.log('Uploading app bundle...');
        const appBundleId = await uploadAppBundle(token);
        
        console.log('Creating activity...');
        const activityId = await createActivity(token, appBundleId);
        
        console.log('\\nDeployment successful!');
        console.log(`App Bundle: ${appBundleId}`);
        console.log(`Activity: ${activityId}`);
        
    } catch (error) {
        console.error('Deployment failed:', error.message);
        process.exit(1);
    }
}

main();