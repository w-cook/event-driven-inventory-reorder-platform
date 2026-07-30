import { useState } from 'react'
import type { FormEvent } from 'react'

import {
  createInventoryItem,
  updateInventoryItem,
} from '../api/inventoryItems'
import type {
  InventoryItem,
  InventoryItemMutationRequest,
} from '../types/inventoryItem'

interface Props {
  itemToEdit: InventoryItem | null
  onSaved: (
    item: InventoryItem,
    wasCreated: boolean,
  ) => void
  onCancelEdit: () => void
}

export function InventoryItemForm({
  itemToEdit,
  onSaved,
  onCancelEdit,
}: Props) {
  const isEditing = itemToEdit !== null

  const [name, setName] = useState(
    itemToEdit?.name ?? '',
  )
  const [sku, setSku] = useState(
    itemToEdit?.sku ?? '',
  )
  const [quantityOnHand, setQuantityOnHand] =
    useState(itemToEdit?.quantityOnHand ?? 0)
  const [reorderThreshold, setReorderThreshold] =
    useState(itemToEdit?.reorderThreshold ?? 0)
  const [reorderQuantity, setReorderQuantity] =
    useState(itemToEdit?.reorderQuantity ?? 1)

  const [isSubmitting, setIsSubmitting] =
    useState(false)
  const [errorMessage, setErrorMessage] =
    useState('')
  const [successMessage, setSuccessMessage] =
    useState('')

  async function handleSubmit(
    event: FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault()

    setIsSubmitting(true)
    setErrorMessage('')
    setSuccessMessage('')

    const request: InventoryItemMutationRequest = {
      name: name.trim(),
      sku: sku.trim(),
      quantityOnHand,
      reorderThreshold,
      reorderQuantity,
    }

    try {
      const savedItem = isEditing
        ? await updateInventoryItem(
            itemToEdit.id,
            request,
          )
        : await createInventoryItem(request)

      onSaved(savedItem, !isEditing)

      setSuccessMessage(
        isEditing
          ? `${savedItem.name} was updated successfully.`
          : `${savedItem.name} was created successfully.`,
      )

      if (!isEditing) {
        setName('')
        setSku('')
        setQuantityOnHand(0)
        setReorderThreshold(0)
        setReorderQuantity(1)
      }
    } catch (error) {
      setErrorMessage(
        error instanceof Error
          ? error.message
          : 'Unable to save the inventory item.',
      )
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <section className="card inventory-management">
      <div className="section-header">
        <div>
          <h2>
            {isEditing
              ? 'Edit Inventory Item'
              : 'Create Inventory Item'}
          </h2>

          <p>
            {isEditing
              ? `Update ${itemToEdit.name} and its reorder configuration.`
              : 'Add a new item and configure its stock and reorder behavior.'}
          </p>
        </div>

        {isEditing && (
          <button
            type="button"
            className="secondary-button"
            onClick={onCancelEdit}
            disabled={isSubmitting}
          >
            Create New Item
          </button>
        )}
      </div>

      {errorMessage && (
        <p className="error">{errorMessage}</p>
      )}

      {successMessage && (
        <p className="success-message">
          {successMessage}
        </p>
      )}

      <form
        className="inventory-form"
        onSubmit={handleSubmit}
      >
        <div className="inventory-form-grid">
          <label>
            Name
            <input
              type="text"
              value={name}
              onChange={(event) =>
                setName(event.target.value)
              }
              maxLength={50}
              required
            />
          </label>

          <label>
            SKU
            <input
              type="text"
              value={sku}
              onChange={(event) =>
                setSku(event.target.value)
              }
              maxLength={50}
              required
            />
          </label>

          <label>
            Quantity on Hand
            <input
              type="number"
              value={quantityOnHand}
              onChange={(event) =>
                setQuantityOnHand(
                  event.target.valueAsNumber,
                )
              }
              min={0}
              step={1}
              required
            />
          </label>

          <label>
            Reorder Threshold
            <input
              type="number"
              value={reorderThreshold}
              onChange={(event) =>
                setReorderThreshold(
                  event.target.valueAsNumber,
                )
              }
              min={0}
              step={1}
              required
            />
          </label>

          <label>
            Reorder Quantity
            <input
              type="number"
              value={reorderQuantity}
              onChange={(event) =>
                setReorderQuantity(
                  event.target.valueAsNumber,
                )
              }
              min={1}
              step={1}
              required
            />
          </label>
        </div>

        <div className="inventory-form-actions">
          <button
            type="submit"
            disabled={isSubmitting}
          >
            {isSubmitting
              ? 'Saving...'
              : isEditing
                ? 'Save Changes'
                : 'Create Item'}
          </button>

          {isEditing && (
            <button
              type="button"
              className="secondary-button"
              onClick={onCancelEdit}
              disabled={isSubmitting}
            >
              Cancel Edit
            </button>
          )}
        </div>
      </form>
    </section>
  )
}