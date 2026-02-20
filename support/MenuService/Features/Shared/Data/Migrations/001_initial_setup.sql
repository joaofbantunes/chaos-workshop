CREATE TABLE public.products
(
    id    text           NOT NULL,
    price numeric(10, 2) NOT NULL,
    CONSTRAINT items_pkey PRIMARY KEY (id)
);