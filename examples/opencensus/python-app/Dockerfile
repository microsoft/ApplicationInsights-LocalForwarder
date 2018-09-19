FROM python:3.6

ENV PYTHONUNBUFFERED 1

RUN apt-get update && apt-get install -y \
  libpq-dev \
  python-dev 

RUN mkdir /code
WORKDIR /code
ADD requirements.txt /code/
RUN pip install -r requirements.txt

ADD . /code/
EXPOSE 8000

CMD /bin/bash -c 'python3 manage.py runserver 0.0.0.0:8000'