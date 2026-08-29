/**
 * CloudBase 云函数 deepseek-proxy —— 无密钥 AI 问答代理（参考实现，未部署）。
 *
 * 客户端合同：POST JSON { "message": "<用户问题>" }
 * 返回合同：OpenAI 兼容 { "choices": [ { "message": { "content": "<回复>" } } ] }；
 *          失败返回 { "choices": [], "error": "<中文原因>" }（客户端按空 choices 提示）。
 *
 * 安全合同：
 * - Key 只从云函数环境变量读取，禁止写入代码、日志或返回值；
 * - 请求体只接受 message 字段，模型/温度/系统提示词由服务端固定，防止参数注入；
 * - 简单限流防刷（同 IP 每 60 秒最多 10 次），生产可换 Redis/计数器。
 *
 * 环境变量（在 CloudBase 控制台"云函数-配置-环境变量"填写）：
 * - DEEPSEEK_API_KEY     必填，服务端轮换后的新 Key
 * - DEEPSEEK_MODEL       选填，默认 deepseek-chat
 * - DEEPSEEK_SYSTEM_PROMPT 选填，默认"铁小探"人设
 */
const https = require('https');

const MODEL = process.env.DEEPSEEK_MODEL || 'deepseek-chat';
const SYSTEM_PROMPT = process.env.DEEPSEEK_SYSTEM_PROMPT ||
    '你是"铁小探"，钢轨探伤仿真教学的 AI 讲师。请用简洁、专业的语言回答钢轨探伤原理、操作技巧、波形解读等问题。';
const TIMEOUT_MS = 30000;
const RATE_LIMIT = { windowMs: 60000, maxPerWindow: 10 };
const _hits = new Map(); // ip -> [timestamps]

function reply(content) {
    return { choices: [{ message: { content } }] };
}
function fail(message) {
    return { choices: [], error: message };
}

function limited(ip) {
    const now = Date.now();
    const hits = (_hits.get(ip) || []).filter(t => now - t < RATE_LIMIT.windowMs);
    hits.push(now);
    _hits.set(ip, hits);
    if (_hits.size > 1000) _hits.clear(); // 粗略防泄漏
    return hits.length > RATE_LIMIT.maxPerWindow;
}

function callDeepSeek(apiKey, message) {
    return new Promise((resolve, reject) => {
        const body = JSON.stringify({
            model: MODEL,
            messages: [
                { role: 'system', content: SYSTEM_PROMPT },
                { role: 'user', content: String(message).slice(0, 2000) },
            ],
            temperature: 1,
        });
        const req = https.request({
            hostname: 'api.deepseek.com',
            path: '/v1/chat/completions',
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                Authorization: 'Bearer ' + apiKey, // 仅服务端内存，不落日志
            },
            timeout: TIMEOUT_MS,
        }, res => {
            let data = '';
            res.on('data', c => (data += c));
            res.on('end', () => {
                try {
                    const json = JSON.parse(data);
                    const content = json.choices && json.choices[0] && json.choices[0].message &&
                        json.choices[0].message.content;
                    if (res.statusCode === 200 && content) resolve(content);
                    else reject(new Error('upstream_' + res.statusCode));
                } catch (e) { reject(new Error('upstream_parse')); }
            });
        });
        req.on('timeout', () => { req.destroy(new Error('upstream_timeout')); });
        req.on('error', reject);
        req.write(body);
        req.end();
    });
}

exports.main = async (event) => {
    if (!process.env.DEEPSEEK_API_KEY) return fail('服务端未配置 AI 凭据');
    const ip = (event && (event.requestContext && event.requestContext.sourceIp)) || 'unknown';
    if (limited(ip)) return fail('提问太频繁，请稍后再试');

    let message = '';
    try {
        const parsed = typeof event.body === 'string' ? JSON.parse(event.body) : (event.body || event);
        message = String((parsed && parsed.message) || '').trim();
    } catch (e) { return fail('请求格式错误'); }
    if (!message) return fail('请输入问题');

    try {
        const content = await callDeepSeek(process.env.DEEPSEEK_API_KEY, message);
        return reply(content);
    } catch (e) {
        console.error('proxy error:', e.message); // 只记错误类别，不记内容与凭据
        return fail('网络连接失败，请检查网络后重试');
    }
};
