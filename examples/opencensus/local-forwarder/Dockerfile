FROM buildpack-deps:jessie-curl

RUN mkdir /lf

RUN curl -L \
    --retry 5 \
    --retry-delay 0 \
    --retry-max-time 40 \
     'https://github.com/Microsoft/ApplicationInsights-LocalForwarder/releases/download/v0.1-beta1/LF-ConsoleHost-linux-x64.tar.gz' \
     -o /lf/LF-ConsoleHost-linux-x64.tar.gz

RUN tar -xvzf /lf/LF-ConsoleHost-linux-x64.tar.gz -C /lf/

RUN chmod +x /lf/Microsoft.LocalForwarder.ConsoleHost

EXPOSE 55678

CMD bash -c 'cd /lf/ && ./Microsoft.LocalForwarder.ConsoleHost noninteractive'

