"""Django settings for test app."""

# Build paths inside the project like this: os.path.join(BASE_DIR, ...)
import os

BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SECRET_KEY = 'secret_key_for_test'

ALLOWED_HOSTS = ['*']

# Application definition
INSTALLED_APPS = (
    'django.contrib.admin',
    'django.contrib.auth',
    'django.contrib.contenttypes',
    'django.contrib.sessions',
    'django.contrib.messages',
    'django.contrib.staticfiles',
    'opencensus.trace.ext.django',
)

MIDDLEWARE_CLASSES = (
    'django.contrib.sessions.middleware.SessionMiddleware',
    'django.middleware.common.CommonMiddleware',
    'django.middleware.csrf.CsrfViewMiddleware',
    'django.contrib.auth.middleware.AuthenticationMiddleware',
    'django.contrib.auth.middleware.SessionAuthenticationMiddleware',
    'django.contrib.messages.middleware.MessageMiddleware',
    'django.middleware.clickjacking.XFrameOptionsMiddleware',
    'django.middleware.security.SecurityMiddleware',
    'opencensus.trace.ext.django.middleware.OpencensusMiddleware',
)

ROOT_URLCONF = 'app.urls'

TEMPLATES = [
    {
        'BACKEND': 'django.template.backends.django.DjangoTemplates',
        'DIRS': [
            os.path.join(BASE_DIR, 'app', 'templates'),
        ],
        'APP_DIRS': True,
        'OPTIONS': {
            'context_processors': [
                'django.template.context_processors.debug',
                'django.template.context_processors.request',
                'django.contrib.auth.context_processors.auth',
                'django.contrib.messages.context_processors.messages',
            ],
        },
    },
]

OPENCENSUS_TRACE = {
    'SAMPLER': 'opencensus.trace.samplers.always_on.AlwaysOnSampler',
    'EXPORTER': 'opencensus.trace.exporters.ocagent.trace_exporter.TraceExporter',
    'PROPAGATOR': 'opencensus.trace.propagation.trace_context_http_header_format.'
                  'TraceContextPropagator',
    'TRANSPORT': 'opencensus.trace.exporters.transports.background_thread.'
                  'BackgroundThreadTransport'
}

OPENCENSUS_TRACE_PARAMS = {
    'SAMPLING_RATE': 1.0,
    'SERVICE_NAME':  os.getenv('SERVICE_NAME', 'python-service'),
    'OCAGENT_TRACE_EXPORTER_ENDPOINT': os.getenv('OCAGENT_TRACE_EXPORTER_ENDPOINT')
}

# Internationalization
# https://docs.djangoproject.com/en/1.8/topics/i18n/

LANGUAGE_CODE = 'en-us'

TIME_ZONE = 'UTC'

USE_I18N = True

USE_L10N = True

USE_TZ = True


# Static files (CSS, JavaScript, Images)
# https://docs.djangoproject.com/en/1.8/howto/static-files/

STATIC_ROOT = 'static'
STATIC_URL = '/static/'

ALLOWED_HOSTS = ['*']
