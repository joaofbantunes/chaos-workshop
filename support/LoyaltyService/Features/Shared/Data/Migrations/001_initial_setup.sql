CREATE TABLE public.orders
(
    id          uuid                     NOT NULL,
    customer_id uuid                     NOT NULL,
    placed_date timestamp with time zone NOT NULL,
    CONSTRAINT orders_pkey PRIMARY KEY (id)
);

CREATE INDEX idx_orders_customer_id ON public.orders (customer_id);