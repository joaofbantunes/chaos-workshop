CREATE TABLE public.orders
(
    id                  uuid                     NOT NULL,
    customer_id         uuid                     NOT NULL,
    discount_percentage numeric(3, 2)            NOT NULL,
    amount              numeric(10, 2)           NOT NULL,
    placed_date         timestamp with time zone NOT NULL,
    CONSTRAINT orders_pkey PRIMARY KEY (id)
);

CREATE TABLE public.order_items
(
    order_id   uuid           NOT NULL,
    product_id    text           NOT NULL,
    quantity   int            NOT NULL,
    unit_price numeric(10, 2) NOT NULL,
    CONSTRAINT order_items_pkey PRIMARY KEY (order_id, product_id),
    CONSTRAINT order_items_order_id_fkey FOREIGN KEY (order_id)
        REFERENCES public.orders (id) MATCH SIMPLE
        ON UPDATE NO ACTION ON DELETE CASCADE
);