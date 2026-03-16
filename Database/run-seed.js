const { MongoClient } = require('mongodb');
const fs = require('fs');
const path = require('path');

const url = 'mongodb://localhost:27017';

const filePath = path.join(__dirname, 'VinhKhanh-Real-Seed.mongodb.js');
let fileContent = fs.readFileSync(filePath, 'utf-8');

const dataStr = fileContent.substring(fileContent.indexOf('const pois = ['), fileContent.indexOf('db.pois.insertMany(pois);'));
const cleanStr = dataStr.replace(/new Date\(\)/g, 'new Date()') + '\nmodule.exports = pois;';

// Write to temp file to easily require
fs.writeFileSync(path.join(__dirname, 'temp.js'), cleanStr);
let rawPois = require('./temp');

// Bổ sung dữ liệu mô phỏng Script Audio (TTS)
rawPois = rawPois.map(poi => {
    poi.Script_Vi = `Chào mừng quý khách đến với ${poi.Name_Vi || 'điểm đến này'}. ${poi.FunFact || poi.Description_Vi?.substring(0, 100) || 'Chúc quý khách có trải nghiệm tuyệt vời'}.`;
    poi.Script_En = `Welcome to ${poi.Name_En || 'this destination'}. ${poi.FunFact || poi.Description_En?.substring(0, 100) || 'Have a great experience'}.`;
    poi.Script_Zh = `欢迎来到${poi.Name_Zh || '这个目的地'}. ${poi.Description_Zh?.substring(0, 50) || '祝您有美好的体验'}.`;
    return poi;
});

async function run() {
    console.log(`Bắt đầu chạy Script insert ${rawPois.length} POI`);
    // Lỗi useNewUrlParser trong thư viện mongodb bản mới, ta bỏ qua args cũ đi.
    const client = new MongoClient(url);
    
    try {
        await client.connect();
        const db = client.db('streetfood_narrator_db');
        const poisCollection = db.collection('pois');
        const countersCollection = db.collection('counters');

        await poisCollection.deleteMany({});
        await countersCollection.updateOne(
            { _id: 'poi_id' },
            { $set: { Value: 0 } },
            { upsert: true }
        );

        if (rawPois.length > 0) {
           await poisCollection.insertMany(rawPois);
           await countersCollection.updateOne(
               { _id: 'poi_id' },
               { $set: { Value: rawPois.length } }
           );
            console.log("Insert POI hoàn tất");
        } else {
             console.log("Không có dữ liệu, kiểm tra lại file js");
        }
    } catch (e) {
        console.error("Lỗi:", e);
    } finally {
        await client.close();
        if (fs.existsSync(path.join(__dirname, 'temp.js'))) {
           fs.unlinkSync(path.join(__dirname, 'temp.js'));
        }
    }
}
run();
