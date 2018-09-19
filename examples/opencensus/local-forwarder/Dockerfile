FROM buildpack-deps:jessie-curl

RUN mkdir /lf

# delete me
ADD LF-ConsoleHost-linux-x64 /lf/

# TODO: fix url to release
#RUN curl -L \
#    --max-time 10 \
#    --connect-timeout 5 \
#    --retry 5 \
#    --retry-delay 0 \
#    --retry-max-time 40 \
#     'https://github.com/Microsoft/ApplicationInsights-LocalForwarder/releases/download/v0.1-alpha2/LF-ConsoleHost-linux-x64.tar.gz' \
#     -o /lf/LF-ConsoleHost-linux-x64.tar.gz

# RUN tar -xvzf /lf/LF-ConsoleHost-linux-x64.tar.gz

RUN chmod +x /lf/Microsoft.LocalForwarder.ConsoleHost

EXPOSE 55678

CMD bash -c 'cd /lf/ && ./Microsoft.LocalForwarder.ConsoleHost noninteractive'

