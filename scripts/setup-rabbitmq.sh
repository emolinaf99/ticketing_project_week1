#!/bin/bash
# Script manual para crear exchanges, colas y bindings en RabbitMQ via API HTTP
# Ejecutar después de: docker compose up -d

# Variables de entorno o defaults
RABBIT_HOST="${RABBITMQ_HOST:-localhost}"
RABBIT_PORT="${RABBITMQ_PORT:-15672}"
RABBIT_USER="${RABBITMQ_DEFAULT_USER:-guest}"
RABBIT_PASS="${RABBITMQ_DEFAULT_PASS:-guest}"
VHOST="%2F"  # "/" URL encoded

RABBIT_URL="http://$RABBIT_HOST:$RABBIT_PORT/api"

echo "Configurando RabbitMQ en http://$RABBIT_HOST:$RABBIT_PORT..."
echo ""

# Exchange
echo "[1/10] Creando exchange: tickets"
curl -s -u "$RABBIT_USER:$RABBIT_PASS" -X PUT \
  -H "content-type:application/json" \
  -d '{"type":"topic","durable":true}' \
  "$RABBIT_URL/exchanges/$VHOST/tickets" && echo " ✓" || echo " ✗"

# Queues
echo "[2/10] Creando queue: q.ticket.reserved"
curl -s -u "$RABBIT_USER:$RABBIT_PASS" -X PUT \
  -H "content-type:application/json" \
  -d '{"durable":true}' \
  "$RABBIT_URL/queues/$VHOST/q.ticket.reserved" && echo " ✓" || echo " ✗"

echo "[3/10] Creando queue: q.ticket.payments.approved"
curl -s -u "$RABBIT_USER:$RABBIT_PASS" -X PUT \
  -H "content-type:application/json" \
  -d '{"durable":true}' \
  "$RABBIT_URL/queues/$VHOST/q.ticket.payments.approved" && echo " ✓" || echo " ✗"

echo "[4/10] Creando queue: q.ticket.payments.rejected"
curl -s -u "$RABBIT_USER:$RABBIT_PASS" -X PUT \
  -H "content-type:application/json" \
  -d '{"durable":true}' \
  "$RABBIT_URL/queues/$VHOST/q.ticket.payments.rejected" && echo " ✓" || echo " ✗"

echo "[5/12] Creando queue: q.ticket.payment.requested"
curl -s -u "$RABBIT_USER:$RABBIT_PASS" -X PUT \
  -H "content-type:application/json" \
  -d '{"durable":true}' \
  "$RABBIT_URL/queues/$VHOST/q.ticket.payment.requested" && echo " ✓" || echo " ✗"

echo "[6/12] Creando queue: q.ticket.status.changed"
curl -s -u "$RABBIT_USER:$RABBIT_PASS" -X PUT \
  -H "content-type:application/json" \
  -d '{"durable":true}' \
  "$RABBIT_URL/queues/$VHOST/q.ticket.status.changed" && echo " ✓" || echo " ✗"

echo "[7/12] Creando queue: q.ticket.expired"
curl -s -u "$RABBIT_USER:$RABBIT_PASS" -X PUT \
  -H "content-type:application/json" \
  -d '{"durable":true}' \
  "$RABBIT_URL/queues/$VHOST/q.ticket.expired" && echo " ✓" || echo " ✗"

echo "[8/12] Creando queue delay: q.ticket.reserved.delay (TTL 1 min)"
curl -s -u "$RABBIT_USER:$RABBIT_PASS" -X PUT \
  -H "content-type:application/json" \
  -d '{"durable":true,"arguments":{"x-message-ttl":60000,"x-dead-letter-exchange":"tickets","x-dead-letter-routing-key":"ticket.expired"}}' \
  "$RABBIT_URL/queues/$VHOST/q.ticket.reserved.delay" && echo " ✓" || echo " ✗"

# Bindings
echo "[9/12] Bindeando: q.ticket.reserved ← ticket.reserved"
curl -s -u "$RABBIT_USER:$RABBIT_PASS" -X POST \
  -H "content-type:application/json" \
  -d '{"routing_key":"ticket.reserved"}' \
  "$RABBIT_URL/bindings/$VHOST/e/tickets/q/q.ticket.reserved" && echo " ✓" || echo " ✗"

echo "[10/12] Bindeando: q.ticket.payments.approved ← ticket.payments.approved"
curl -s -u "$RABBIT_USER:$RABBIT_PASS" -X POST \
  -H "content-type:application/json" \
  -d '{"routing_key":"ticket.payments.approved"}' \
  "$RABBIT_URL/bindings/$VHOST/e/tickets/q/q.ticket.payments.approved" && echo " ✓" || echo " ✗"

echo "[11/12] Bindeando: q.ticket.payments.rejected ← ticket.payments.rejected"
curl -s -u "$RABBIT_USER:$RABBIT_PASS" -X POST \
  -H "content-type:application/json" \
  -d '{"routing_key":"ticket.payments.rejected"}' \
  "$RABBIT_URL/bindings/$VHOST/e/tickets/q/q.ticket.payments.rejected" && echo " ✓" || echo " ✗"

echo "[12/14] Bindeando: q.ticket.payment.requested ← ticket.payment.requested"
curl -s -u "$RABBIT_USER:$RABBIT_PASS" -X POST \
  -H "content-type:application/json" \
  -d '{"routing_key":"ticket.payment.requested"}' \
  "$RABBIT_URL/bindings/$VHOST/e/tickets/q/q.ticket.payment.requested" && echo " ✓" || echo " ✗"

echo "[13/14] Bindeando: q.ticket.status.changed ← ticket.status.changed"
curl -s -u "$RABBIT_USER:$RABBIT_PASS" -X POST \
  -H "content-type:application/json" \
  -d '{"routing_key":"ticket.status.changed"}' \
  "$RABBIT_URL/bindings/$VHOST/e/tickets/q/q.ticket.status.changed" && echo " ✓" || echo " ✗"

echo "[14/16] Bindeando: q.ticket.expired ← ticket.expired"
curl -s -u "$RABBIT_USER:$RABBIT_PASS" -X POST \
  -H "content-type:application/json" \
  -d '{"routing_key":"ticket.expired"}' \
  "$RABBIT_URL/bindings/$VHOST/e/tickets/q/q.ticket.expired" && echo " ✓" || echo " ✗"

# Fair Queue — cola dedicada para que FairQueueService detecte tickets liberados
echo "[15/16] Creando queue: q.ticket.released.queue (Fair Queue listener)"
curl -s -u "$RABBIT_USER:$RABBIT_PASS" -X PUT \
  -H "content-type:application/json" \
  -d '{"durable":true}' \
  "$RABBIT_URL/queues/$VHOST/q.ticket.released.queue" && echo " ✓" || echo " ✗"

echo "[16/16] Bindeando: q.ticket.released.queue ← ticket.status.changed"
curl -s -u "$RABBIT_USER:$RABBIT_PASS" -X POST \
  -H "content-type:application/json" \
  -d '{"routing_key":"ticket.status.changed"}' \
  "$RABBIT_URL/bindings/$VHOST/e/tickets/q/q.ticket.released.queue" && echo " ✓" || echo " ✗"

echo ""
echo "✓ Configuración completada!"
echo ""
echo "Verificando resultado:"
curl -s -u "$RABBIT_USER:$RABBIT_PASS" "$RABBIT_URL/queues/$VHOST" | jq '.[] | {name: .name, durable: .durable, messages: .messages}'
