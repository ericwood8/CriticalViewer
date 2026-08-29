# Built for local dev - runs the Vite dev server with hot reload against
# a bind-mounted source tree (see docker-compose.yml's frontend-ui
# volumes): no minification, source maps on, slower startup than a real
# production build. Currently also what's pushed to Lightsail Container
# Service as my-react-ui for early testing - replace with a proper
# production build (`vite build` served by something like nginx) before
# this matters for real traffic.
#
#   docker build -t my-react-ui:local .
#   (docker-compose.yml does this for you)

FROM node:22-alpine
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

EXPOSE 5173
# --host so the dev server binds 0.0.0.0, not just localhost - otherwise
# it's unreachable from outside the container.
CMD ["npm", "run", "dev", "--", "--host"]
