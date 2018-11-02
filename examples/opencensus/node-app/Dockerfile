FROM node:10-alpine

RUN mkdir /app
WORKDIR /app

COPY package*.json ./
RUN npm install

ADD . .

EXPOSE 8008
CMD ["npm", "start"]
