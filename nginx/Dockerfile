# The single public-facing container in the Lightsail deployment - see
# templates/default.conf.template for why this exists and how it routes.
#
#   docker build -t my-nginx-proxy:local .
#   (docker-compose.yml does this for you)

FROM nginx:alpine
# Goes in /etc/nginx/templates/, not straight to conf.d - the official
# image's own entrypoint script envsubst's *.template files in this
# directory into conf.d automatically, which is what makes
# ${NGINX_LOCAL_RESOLVERS} in the template resolve to a real value at
# container startup (see the template's own comments for why that
# matters).
COPY templates/default.conf.template /etc/nginx/templates/default.conf.template
