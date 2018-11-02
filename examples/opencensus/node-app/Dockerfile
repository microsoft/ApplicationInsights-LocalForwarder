FROM node:10-alpine

RUN mkdir /work
WORKDIR /work

COPY package*.json ./
# Use --legacy-bundling to work around buggy path in opencensus ocagent.js
RUN npm install --legacy-bundling
# The opencensushack/protos/... folder exists to work around the buggy path also.
COPY opencensushack ./node_modules/@opencensus/exporter-ocagent/src

COPY app ./app

EXPOSE 8008
CMD ["npm", "start"]
