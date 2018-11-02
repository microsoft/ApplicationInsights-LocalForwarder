const http=require('http');

console.log("Starting.");
http.createServer((req, res) => {
    res.end("Hello Earth!");
    console.log("We got a call! :)");
}).listen(8008);
console.log("Started.");
